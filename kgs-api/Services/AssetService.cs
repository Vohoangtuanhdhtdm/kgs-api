using kgs_api.Domain.Entity;
using kgs_api.Domain.ValueObjects;
using kgs_api.Dtos;
using kgs_api.Repositories;
using kgs_api.Storage;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using static kgs_api.Common.Common;
using static kgs_api.Domain.Enums;

namespace kgs_api.Services
{
    public interface IAssetService
    {
        Task<AssetDetailDto> CreateAsync(AssetCreateRequest request, CancellationToken ct = default);
        Task<AssetDetailDto> UpdateAsync(Guid assetId, AssetUpdateRequest request, CancellationToken ct = default);
        Task DeleteAsync(Guid assetId, CancellationToken ct = default);
        Task<AssetDetailDto> GetByIdAsync(Guid assetId, CancellationToken ct = default);
        Task<PagedResult<AssetSummaryDto>> SearchAsync(AssetSearchQuery query, CancellationToken ct = default);
        Task<IReadOnlyList<AssetNearbyDto>> FindNearbyAsync(NearbyQuery query, CancellationToken ct = default);
        Task LinkPropertyAsync(Guid assetId, int propertyId, CancellationToken ct = default);
        Task UnlinkPropertyAsync(Guid assetId, CancellationToken ct = default);
    }

    public sealed class AssetService : IAssetService
    {
        private readonly IRepository<Asset> _assets;
        private readonly IRepository<Property> _properties;
        private readonly IUnitOfWork _uow;
        private readonly IFileStorageService _files;
        private readonly ICurrentUserService _currentUser;
        private readonly GeometryFactory _geometryFactory; // singleton SRID 4326 từ DI

        public AssetService(
            IRepository<Asset> assets,
            IRepository<Property> properties,
            IUnitOfWork uow,
            IFileStorageService files,
            ICurrentUserService currentUser,
            GeometryFactory geometryFactory)
        {
            _assets = assets;
            _properties = properties;
            _uow = uow;
            _files = files;
            _currentUser = currentUser;
            _geometryFactory = geometryFactory;
        }

        // -------------------- A1. CRUD --------------------

        public async Task<AssetDetailDto> CreateAsync(AssetCreateRequest request, CancellationToken ct = default)
        {
            var asset = new Asset
            {
                UserId = _currentUser.UserId,
                Name = request.Name.Trim(),
                TypeProperty = request.TypeProperty,
                OwnershipType = request.OwnershipType,
                Status = AssetStatus.InUse,
                Address = MapAddress(request.Address),
                Location = ToPoint(request.Location),
                Area = request.Area,
                CurrentValue = request.CurrentValue,
                AcquisitionDate = request.AcquisitionDate,
                Notes = request.Notes
            };

            await _assets.AddAsync(asset, ct);
            await _uow.SaveChangesAsync(ct);
            return await GetByIdAsync(asset.Id, ct);
        }

        public async Task<AssetDetailDto> UpdateAsync(Guid assetId, AssetUpdateRequest request, CancellationToken ct = default)
        {
            var asset = await GetOwnedAssetAsync(assetId, ct);

            asset.Name = request.Name.Trim();
            asset.TypeProperty = request.TypeProperty;
            asset.Status = request.Status;
            asset.Address = MapAddress(request.Address);
            asset.Location = ToPoint(request.Location);
            asset.Area = request.Area;
            asset.CurrentValue = request.CurrentValue;
            asset.AcquisitionDate = request.AcquisitionDate;
            asset.Notes = request.Notes;

            await _uow.SaveChangesAsync(ct);
            return await GetByIdAsync(assetId, ct);
        }

        public async Task DeleteAsync(Guid assetId, CancellationToken ct = default)
        {
            var asset = await _assets.Query()
                .Include(a => a.Media)
                .Include(a => a.Documents)
                .FirstOrDefaultAsync(a => a.Id == assetId && a.UserId == _currentUser.UserId, ct)
                ?? throw new NotFoundException("Không tìm thấy tài sản.");

            var hasActiveContract = await _assets.Query()
                .Where(a => a.Id == assetId)
                .SelectMany(a => a.Contracts)
                .AnyAsync(c => c.Status == ContractStatus.Active, ct);
            if (hasActiveContract)
                throw new ConflictException("Tài sản còn hợp đồng đang hiệu lực — hãy chấm dứt hợp đồng trước khi xoá.");

            // Đẩy toàn bộ file Cloudinary vào outbox — cùng transaction với DELETE
            _files.ScheduleDeletion(asset.Thumbnail);
            foreach (var m in asset.Media) _files.ScheduleDeletion(m.File);
            foreach (var d in asset.Documents) _files.ScheduleDeletion(d.File);

            _assets.Remove(asset); // các bảng con Cascade theo FK
            await _uow.SaveChangesAsync(ct);
        }

        public async Task<AssetDetailDto> GetByIdAsync(Guid assetId, CancellationToken ct = default)
        {
            var userId = _currentUser.UserId;

            var dto = await _assets.Query().AsNoTracking()
                .Where(a => a.Id == assetId && a.UserId == userId)
                .Select(a => new AssetDetailDto(
                    a.Id, a.Name, a.TypeProperty, a.OwnershipType, a.Status,
                    new AddressDto(a.Address.City, a.Address.District, a.Address.Ward, a.Address.Detail),
                    a.Location == null ? null : new GeoPointDto(a.Location.Y, a.Location.X), // Y=lat, X=lng
                    a.Area, a.CurrentValue, a.AcquisitionDate, a.Notes,
                    a.Thumbnail == null ? null
                        : new StoredFileDto(a.Thumbnail.Url, a.Thumbnail.FileName, a.Thumbnail.ContentType, a.Thumbnail.SizeBytes),
                    a.LinkedPropertyId,
                    a.Units.Count,
                    a.Contracts.Count(c => c.Status == ContractStatus.Active),
                    a.CreatedAt, a.UpdatedAt))
                .FirstOrDefaultAsync(ct);

            return dto ?? throw new NotFoundException("Không tìm thấy tài sản.");
        }

        public async Task<PagedResult<AssetSummaryDto>> SearchAsync(AssetSearchQuery query, CancellationToken ct = default)
        {
            var q = _assets.Query().AsNoTracking()
                .Where(a => a.UserId == _currentUser.UserId);

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var kw = $"%{query.Keyword.Trim()}%";
                q = q.Where(a => EF.Functions.ILike(a.Name, kw)
                              || EF.Functions.ILike(a.Address.Detail, kw));
            }
            if (query.TypeProperty is not null) q = q.Where(a => a.TypeProperty == query.TypeProperty);
            if (query.Status is not null) q = q.Where(a => a.Status == query.Status);
            if (query.OwnershipType is not null) q = q.Where(a => a.OwnershipType == query.OwnershipType);
            if (!string.IsNullOrWhiteSpace(query.City)) q = q.Where(a => a.Address.City == query.City);

            var total = await q.CountAsync(ct);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var page = Math.Max(query.Page, 1);

            var items = await q
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AssetSummaryDto(
                    a.Id, a.Name, a.TypeProperty, a.OwnershipType, a.Status,
                    a.Address.City, a.Address.District, a.CurrentValue,
                    a.Thumbnail == null ? null : a.Thumbnail.Url,
                    a.LinkedPropertyId))
                .ToListAsync(ct);

            return new PagedResult<AssetSummaryDto>(items, page, pageSize, total);
        }

        // -------------------- A2. NEARBY (PostGIS) --------------------

        public async Task<IReadOnlyList<AssetNearbyDto>> FindNearbyAsync(NearbyQuery query, CancellationToken ct = default)
        {
            // LƯU Ý: Coordinate(X = longitude, Y = latitude)
            var origin = _geometryFactory.CreatePoint(new Coordinate(query.Longitude, query.Latitude));

            // Với cột geography: IsWithinDistance → ST_DWithin (theo MÉT, dùng GiST index),
            // Distance → ST_Distance (mét). Toàn bộ chạy trong PostgreSQL, không kéo dữ liệu về app.
            return await _assets.Query().AsNoTracking()
                .Where(a => a.UserId == _currentUser.UserId
                         && a.Location != null
                         && a.Location.IsWithinDistance(origin, query.RadiusMeters))
                .OrderBy(a => a.Location!.Distance(origin))
                .Take(Math.Clamp(query.Limit, 1, 100))
                .Select(a => new AssetNearbyDto(
                    a.Id, a.Name, a.TypeProperty, a.Status,
                    a.Location!.Y, a.Location.X,
                    a.Location.Distance(origin)))
                .ToListAsync(ct);
        }

        // -------------------- A3. LINK PROPERTY --------------------

        public async Task LinkPropertyAsync(Guid assetId, int propertyId, CancellationToken ct = default)
        {
            var asset = await GetOwnedAssetAsync(assetId, ct);

            var ownsProperty = await _properties.Query()
                .AnyAsync(p => p.Id == propertyId && p.UserId == _currentUser.UserId, ct);
            if (!ownsProperty)
                throw new NotFoundException("Không tìm thấy tin đăng hoặc tin đăng không thuộc về bạn.");

            var alreadyLinked = await _assets.Query()
                .AnyAsync(a => a.LinkedPropertyId == propertyId && a.Id != assetId, ct);
            if (alreadyLinked)
                throw new ConflictException("Tin đăng này đã được liên kết với một tài sản khác.");

            asset.LinkedPropertyId = propertyId;
            await _uow.SaveChangesAsync(ct);
        }

        public async Task UnlinkPropertyAsync(Guid assetId, CancellationToken ct = default)
        {
            var asset = await GetOwnedAssetAsync(assetId, ct);
            asset.LinkedPropertyId = null;
            await _uow.SaveChangesAsync(ct);
        }

        // -------------------- Helpers --------------------

        private async Task<Asset> GetOwnedAssetAsync(Guid assetId, CancellationToken ct)
            => await _assets.Query()
                   .FirstOrDefaultAsync(a => a.Id == assetId && a.UserId == _currentUser.UserId, ct)
               ?? throw new NotFoundException("Không tìm thấy tài sản.");

        private static Address MapAddress(AddressDto dto) => new()
        {
            City = dto.City.Trim(),
            District = dto.District.Trim(),
            Ward = dto.Ward.Trim(),
            Detail = dto.Detail?.Trim() ?? string.Empty
        };

        /// <summary>Điểm chuyển đổi DUY NHẤT lat/lng → Point để không bao giờ đảo trục.</summary>
        private Point? ToPoint(GeoPointDto? dto)
            => dto is null ? null
               : _geometryFactory.CreatePoint(new Coordinate(dto.Longitude, dto.Latitude));
    }
}
