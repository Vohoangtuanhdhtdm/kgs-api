using kgs_api.Common.Filters;
using kgs_api.Data;
using kgs_api.Domain.Entity;
using kgs_api.Domain.Entity.SubEntity;
using kgs_api.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using static kgs_api.Common.Common;
using static kgs_api.Domain.Enums;


namespace kgs_api.Controllers
{
    [ApiController]
    [Authorize]                      // cần đăng nhập — seed vào đúng tài khoản đang gọi
    [DevelopmentOnly]
    [Route("api/dev/seed")]
    public sealed class SeedController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUser;
        private readonly GeometryFactory _geometryFactory;
        private readonly IWebHostEnvironment _env;

        public SeedController(
            ApplicationDbContext db,
            ICurrentUserService currentUser,
            GeometryFactory geometryFactory,
            IWebHostEnvironment env)
        {
            _db = db;
            _currentUser = currentUser;
            _geometryFactory = geometryFactory;
            _env = env;
        }

        /// <summary>
        /// POST /api/dev/seed
        /// Tạo bộ dữ liệu mẫu đầy đủ cho user đang đăng nhập:
        ///  - 3 tài sản (1 Owned cho thuê nguyên căn, 1 Leasehold nhiều phòng, 1 Owned đang rao bán)
        ///  - 4 đối tác (người thuê, chủ nhà, môi giới, nhà thầu)
        ///  - 3 hợp đồng (2 chiều LeaseOut + 1 chiều LeaseIn)
        ///  - Bút toán thu/chi + thuế trải đều 6 tháng gần nhất
        ///  - Reminder, thiết bị, sửa chữa, giấy tờ sắp hết hạn
        /// Gọi lại nhiều lần sẽ TỪ CHỐI nếu đã có dữ liệu (tránh nhân bản) — dùng ?force=true để xoá sạch và tạo lại.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Seed([FromQuery] bool force = false, CancellationToken ct = default)
        {
            if (!_env.IsDevelopment())
                return NotFound();

            var userId = _currentUser.UserId;

            var existing = await _db.Assets.CountAsync(a => a.UserId == userId, ct);
            if (existing > 0 && !force)
                return Conflict(new
                {
                    message = $"Tài khoản đã có {existing} tài sản. Gọi lại với ?force=true để xoá sạch và tạo lại.",
                    existingAssets = existing
                });

            if (force && existing > 0)
            {
                var oldAssets = await _db.Assets.Where(a => a.UserId == userId).ToListAsync(ct);
                _db.Assets.RemoveRange(oldAssets);                                  // cascade xoá con
                var oldContacts = await _db.ContactParties.Where(c => c.UserId == userId).ToListAsync(ct);
                var oldReminders = await _db.Reminders.Where(r => r.UserId == userId).ToListAsync(ct);
                _db.Reminders.RemoveRange(oldReminders);
                await _db.SaveChangesAsync(ct);                                     // xoá asset trước
                _db.ContactParties.RemoveRange(oldContacts);                        // rồi mới xoá contact (FK Restrict)
                await _db.SaveChangesAsync(ct);
            }

            var now = DateTime.UtcNow;

            // ---------- 1. ĐỐI TÁC ----------
            var tenant = new ContactParty
            {
                UserId = userId,
                Type = ContactType.Tenant,
                FullName = "Nguyễn Văn An",
                Phone = "0901234567",
                Email = "an.nguyen@example.com"
            };
            var landlord = new ContactParty
            {
                UserId = userId,
                Type = ContactType.Landlord,
                FullName = "Trần Thị Bình",
                Phone = "0912345678"
            };
            var broker = new ContactParty
            {
                UserId = userId,
                Type = ContactType.Broker,
                FullName = "Lê Minh Cường",
                Phone = "0923456789",
                Email = "cuong.le@example.com"
            };
            var vendor = new ContactParty
            {
                UserId = userId,
                Type = ContactType.Vendor,
                FullName = "Cơ sở sửa chữa Đại Phát",
                Phone = "0934567890"
            };
            await _db.ContactParties.AddRangeAsync(new[] { tenant, landlord, broker, vendor }, ct);

            // ---------- 2. TÀI SẢN ----------
            // (a) Nhà phố Quận 7 — Owned, đang cho thuê nguyên căn
            var asset1 = new Asset
            {
                UserId = userId,
                Name = "Nhà phố Quận 7",
                TypeProperty = AssetDomainType.PrivateHouse,
                OwnershipType = AssetOwnershipType.Owned,
                Status = AssetStatus.RentedOut,
                Address = new Address { City = "TP. Hồ Chí Minh", District = "Quận 7", Ward = "Phường Tân Phong", Detail = "123 Nguyễn Thị Thập" },
                Location = Point(106.7219, 10.7300),
                Area = 120,
                CurrentValue = 8_500_000_000m,
                AcquisitionDate = now.AddYears(-5),
                Notes = "Nhà 3 tầng, hướng Đông Nam."
            };

            // (b) Chung cư mini Bình Thạnh — Leasehold, thuê rồi cho thuê lại từng phòng
            var asset2 = new Asset
            {
                UserId = userId,
                Name = "Chung cư mini Bình Thạnh",
                TypeProperty = AssetDomainType.Apartment,
                OwnershipType = AssetOwnershipType.Leasehold,
                Status = AssetStatus.RentedOut,
                Address = new Address { City = "TP. Hồ Chí Minh", District = "Quận Bình Thạnh", Ward = "Phường 25", Detail = "45 Điện Biên Phủ" },
                Location = Point(106.7100, 10.8010),
                Area = 200,
                AcquisitionDate = now.AddMonths(-8),
                Notes = "Thuê nguyên căn từ chủ, chia 4 phòng cho thuê lại."
            };

            // (c) Đất nền Thủ Đức — Owned, đang rao bán
            var asset3 = new Asset
            {
                UserId = userId,
                Name = "Đất nền TP. Thủ Đức",
                TypeProperty = AssetDomainType.Land,
                OwnershipType = AssetOwnershipType.Owned,
                Status = AssetStatus.ForSale,
                Address = new Address { City = "TP. Hồ Chí Minh", District = "TP. Thủ Đức", Ward = "Phường Long Trường", Detail = "Lô A12, KDC Nam Long" },
                Location = Point(106.8080, 10.8100),
                Area = 100,
                CurrentValue = 4_200_000_000m,
                AcquisitionDate = now.AddYears(-3)
            };

            await _db.Assets.AddRangeAsync(new[] { asset1, asset2, asset3 }, ct);

            // ---------- 3. PHÒNG (cho asset2) ----------
            var unit1 = new AssetUnit { Asset = asset2, Name = "Phòng 101", FloorNumber = 1, Area = 30, Status = UnitStatus.Occupied };
            var unit2 = new AssetUnit { Asset = asset2, Name = "Phòng 102", FloorNumber = 1, Area = 30, Status = UnitStatus.Vacant };
            var unit3 = new AssetUnit { Asset = asset2, Name = "Phòng 201", FloorNumber = 2, Area = 35, Status = UnitStatus.Occupied };
            var unit4 = new AssetUnit { Asset = asset2, Name = "Phòng 202", FloorNumber = 2, Area = 35, Status = UnitStatus.UnderMaintenance };
            await _db.AssetUnits.AddRangeAsync(new[] { unit1, unit2, unit3, unit4 }, ct);

            // ---------- 4. HỢP ĐỒNG ----------
            // (a) LeaseOut nguyên căn asset1 — sắp hết hạn trong 20 ngày (để test cảnh báo)
            var contract1 = new LeaseContract
            {
                Asset = asset1,
                AssetUnitId = null,
                Direction = ContractDirection.LeaseOut,
                Status = ContractStatus.Active,
                Counterparty = tenant,
                StartDate = now.AddMonths(-11),
                EndDate = now.AddDays(20),
                RentAmount = 25_000_000m,
                PaymentCycle = PaymentCycle.Monthly,
                PaymentDueDay = 5,
                DepositAmount = 50_000_000m,
                TaxResponsibility = TaxResponsibility.Landlord,
                Notes = "Hợp đồng 12 tháng, thanh toán đầu tháng."
            };

            // (b) LeaseIn — user thuê asset2 từ chủ nhà
            var contract2 = new LeaseContract
            {
                Asset = asset2,
                AssetUnitId = null,
                Direction = ContractDirection.LeaseIn,
                Status = ContractStatus.Active,
                Counterparty = landlord,
                StartDate = now.AddMonths(-8),
                EndDate = now.AddMonths(16),
                RentAmount = 30_000_000m,
                PaymentCycle = PaymentCycle.Monthly,
                PaymentDueDay = 1,
                DepositAmount = 60_000_000m,
                TaxResponsibility = TaxResponsibility.Landlord,
                Notes = "Thuê nguyên căn 24 tháng."
            };

            // (c) LeaseOut phòng 101 của asset2
            var contract3 = new LeaseContract
            {
                Asset = asset2,
                AssetUnit = unit1,
                Direction = ContractDirection.LeaseOut,
                Status = ContractStatus.Active,
                Counterparty = tenant,
                StartDate = now.AddMonths(-6),
                EndDate = now.AddMonths(6),
                RentAmount = 6_500_000m,
                PaymentCycle = PaymentCycle.Monthly,
                PaymentDueDay = 10,
                DepositAmount = 6_500_000m,
                TaxResponsibility = TaxResponsibility.Landlord
            };

            await _db.LeaseContracts.AddRangeAsync(new[] { contract1, contract2, contract3 }, ct);

            // ---------- 5. SỔ THU CHI — 6 tháng gần nhất ----------
            var cashFlows = new List<CashFlowEntry>();
            for (int i = 5; i >= 0; i--)
            {
                var month = now.AddMonths(-i);

                // Thu tiền thuê nguyên căn asset1
                cashFlows.Add(new CashFlowEntry
                {
                    UserId = userId,
                    Asset = asset1,
                    LeaseContract = contract1,
                    Direction = CashFlowDirection.Income,
                    Category = CashFlowCategory.RentIncome,
                    Amount = 25_000_000m,
                    OccurredAt = month,
                    PeriodStart = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                    PeriodEnd = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1).AddDays(-1),
                    Description = $"Tiền thuê tháng {month:MM/yyyy} — Nhà phố Quận 7"
                });

                // Chi tiền thuê cho chủ nhà asset2
                cashFlows.Add(new CashFlowEntry
                {
                    UserId = userId,
                    Asset = asset2,
                    LeaseContract = contract2,
                    Direction = CashFlowDirection.Expense,
                    Category = CashFlowCategory.RentExpense,
                    Amount = 30_000_000m,
                    OccurredAt = month,
                    Description = $"Trả tiền thuê chủ nhà tháng {month:MM/yyyy}"
                });

                // Thu tiền thuê phòng 101
                cashFlows.Add(new CashFlowEntry
                {
                    UserId = userId,
                    Asset = asset2,
                    AssetUnit = unit1,
                    LeaseContract = contract3,
                    Direction = CashFlowDirection.Income,
                    Category = CashFlowCategory.RentIncome,
                    Amount = 6_500_000m,
                    OccurredAt = month,
                    Description = $"Tiền thuê phòng 101 tháng {month:MM/yyyy}"
                });

                // Hoá đơn điện nước
                cashFlows.Add(new CashFlowEntry
                {
                    UserId = userId,
                    Asset = asset2,
                    Direction = CashFlowDirection.Expense,
                    Category = CashFlowCategory.ElectricityBill,
                    Amount = 2_800_000m + (i * 100_000),
                    OccurredAt = month.AddDays(3),
                    Description = $"Tiền điện tháng {month:MM/yyyy}"
                });
                cashFlows.Add(new CashFlowEntry
                {
                    UserId = userId,
                    Asset = asset2,
                    Direction = CashFlowDirection.Expense,
                    Category = CashFlowCategory.WaterBill,
                    Amount = 900_000m,
                    OccurredAt = month.AddDays(3),
                    Description = $"Tiền nước tháng {month:MM/yyyy}"
                });
            }

            // Thuế trong năm — để test báo cáo thuế theo năm
            var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            cashFlows.Add(new CashFlowEntry
            {
                UserId = userId,
                Asset = asset1,
                Direction = CashFlowDirection.Expense,
                Category = CashFlowCategory.BusinessLicenseTax,
                Amount = 1_000_000m,
                OccurredAt = yearStart.AddDays(20),
                Description = "Thuế môn bài năm " + now.Year
            });
            cashFlows.Add(new CashFlowEntry
            {
                UserId = userId,
                Asset = asset1,
                Direction = CashFlowDirection.Expense,
                Category = CashFlowCategory.PersonalIncomeTax,
                Amount = 15_000_000m,
                OccurredAt = yearStart.AddMonths(1),
                Description = "Thuế TNCN 5% tiền cho thuê"
            });
            cashFlows.Add(new CashFlowEntry
            {
                UserId = userId,
                Asset = asset1,
                Direction = CashFlowDirection.Expense,
                Category = CashFlowCategory.ValueAddedTax,
                Amount = 15_000_000m,
                OccurredAt = yearStart.AddMonths(1),
                Description = "Thuế GTGT 5% tiền cho thuê"
            });
            cashFlows.Add(new CashFlowEntry
            {
                UserId = userId,
                Asset = asset3,
                Direction = CashFlowDirection.Expense,
                Category = CashFlowCategory.NonAgriculturalLandTax,
                Amount = 2_400_000m,
                OccurredAt = yearStart.AddMonths(2),
                Description = "Thuế sử dụng đất phi nông nghiệp"
            });

            await _db.CashFlowEntries.AddRangeAsync(cashFlows, ct);

            // ---------- 6. NHẮC LỊCH ----------
            var reminders = new List<Reminder>
            {
                new() {
                    UserId = userId, Asset = asset1, LeaseContract = contract1,
                    Type = ReminderType.RentCollection, Title = "Thu tiền thuê: Nhà phố Quận 7",
                    DueDate = NextMonthDay(now, 5), Cycle = RecurrenceCycle.Monthly, NotifyDaysBefore = 3, IsActive = true
                },
                new() {
                    UserId = userId, Asset = asset1, LeaseContract = contract1,
                    Type = ReminderType.ContractExpiry, Title = "Hợp đồng sắp hết hạn: Nhà phố Quận 7",
                    DueDate = contract1.EndDate, Cycle = RecurrenceCycle.None, NotifyDaysBefore = 30, IsActive = true
                },
                new() {
                    UserId = userId, Asset = asset2, LeaseContract = contract2,
                    Type = ReminderType.RentPayment, Title = "Đóng tiền thuê cho chủ nhà: Chung cư mini Bình Thạnh",
                    DueDate = NextMonthDay(now, 1), Cycle = RecurrenceCycle.Monthly, NotifyDaysBefore = 3, IsActive = true
                },
                new() {
                    UserId = userId, Asset = asset1,
                    Type = ReminderType.TaxDue, Title = "Đóng thuế môn bài năm sau",
                    DueDate = new DateTime(now.Year + 1, 1, 30, 0, 0, 0, DateTimeKind.Utc),
                    Cycle = RecurrenceCycle.Annually, NotifyDaysBefore = 15, IsActive = true
                },
                new() {
                    UserId = userId, Asset = asset2,
                    Type = ReminderType.Maintenance, Title = "Bảo dưỡng máy lạnh định kỳ",
                    DueDate = now.AddDays(5), Cycle = RecurrenceCycle.SemiAnnually, NotifyDaysBefore = 7, IsActive = true
                }
            };
            await _db.Reminders.AddRangeAsync(reminders, ct);

            // ---------- 7. THIẾT BỊ ----------
            await _db.Equipments.AddRangeAsync(new[]
            {
                new Equipment { Asset = asset1, Name = "Máy lạnh Daikin 1.5HP", Quantity = 3, Condition = EquipmentCondition.Good, Source = EquipmentSource.OwnerProvided },
                new Equipment { Asset = asset1, Name = "Máy nước nóng Ariston", Quantity = 2, Condition = EquipmentCondition.Fair, Source = EquipmentSource.OwnerProvided },
                new Equipment { Asset = asset2, AssetUnit = unit1, Name = "Giường + tủ quần áo", Quantity = 1, Condition = EquipmentCondition.Good, Source = EquipmentSource.SelfEquipped },
                new Equipment { Asset = asset2, Name = "Máy giặt chung Electrolux", Quantity = 1, Condition = EquipmentCondition.NeedRepair, Source = EquipmentSource.FromLandlord }
            }, ct);

            // ---------- 8. SỬA CHỮA ----------
            await _db.MaintenanceRecords.AddRangeAsync(new[]
            {
                new MaintenanceRecord
                {
                    Asset = asset2, AssetUnit = unit4,
                    Title = "Sửa thấm trần phòng 202", Description = "Chống thấm lại trần, sơn lại tường",
                    StartDate = now.AddDays(-10), CompletedDate = null,
                    Cost = 8_500_000m, Vendor = vendor
                },
                new MaintenanceRecord
                {
                    Asset = asset1,
                    Title = "Thay bồn nước inox", StartDate = now.AddMonths(-3), CompletedDate = now.AddMonths(-3).AddDays(2),
                    Cost = 12_000_000m, Vendor = vendor
                }
            }, ct);

            // ---------- 9. GIẤY TỜ (có cái sắp hết hạn để test cảnh báo) ----------
            await _db.AssetDocuments.AddRangeAsync(new[]
            {
                new AssetDocument
                {
                    Asset = asset1, Type = DocumentType.LandTitle, Title = "Sổ hồng nhà phố Quận 7",
                    File = new StoredFile { Url = "https://placeholder.example/sohong.pdf", PublicId = "seed/sohong-q7", FileName = "so-hong.pdf", ContentType = "application/pdf" },
                    IssueDate = now.AddYears(-5)
                },
                new AssetDocument
                {
                    Asset = asset1, Type = DocumentType.ElectricityContract, Title = "Hợp đồng điện lực",
                    File = new StoredFile { Url = "https://placeholder.example/hd-dien.pdf", PublicId = "seed/hd-dien", FileName = "hd-dien.pdf", ContentType = "application/pdf" },
                    IssueDate = now.AddYears(-2), ExpiryDate = now.AddDays(25)      // sắp hết hạn
                },
                new AssetDocument
                {
                    Asset = asset2, LeaseContract = contract2, Type = DocumentType.LeaseContract, Title = "HĐ thuê nguyên căn từ chủ nhà",
                    File = new StoredFile { Url = "https://placeholder.example/hd-thue.pdf", PublicId = "seed/hd-thue", FileName = "hd-thue.pdf", ContentType = "application/pdf" },
                    IssueDate = now.AddMonths(-8), ExpiryDate = contract2.EndDate
                }
            }, ct);

            // ---------- 10. LỊCH SỬ SỬ DỤNG + RAO BÁN ----------
            await _db.UsagePeriods.AddRangeAsync(new[]
            {
                new UsagePeriod { Asset = asset1, OccupantType = OccupantType.Self, StartDate = now.AddYears(-5), EndDate = now.AddMonths(-11), Notes = "Gia đình ở trước khi cho thuê" },
                new UsagePeriod { Asset = asset1, OccupantType = OccupantType.Tenant, OccupantName = "Nguyễn Văn An", StartDate = now.AddMonths(-11), EndDate = null }
            }, ct);

            var saleListing = new SaleListing
            {
                Asset = asset3,
                AskingPrice = 4_500_000_000m,
                Status = SaleListingStatus.Active,
                ListedAt = now.AddDays(-15),
                AgreementNotes = "Thương lượng, hỗ trợ sang tên."
            };
            await _db.SaleListings.AddAsync(saleListing, ct);
            await _db.SaleListingBrokers.AddAsync(new SaleListingBroker
            {
                SaleListing = saleListing,
                Broker = broker,
                SentAt = now.AddDays(-14),
                Notes = "Đã gửi thông tin, đang tìm khách."
            }, ct);

            await _db.SaveChangesAsync(ct);

            return Ok(new
            {
                message = "Đã tạo dữ liệu mẫu thành công.",
                created = new
                {
                    assets = 3,
                    assetUnits = 4,
                    contacts = 4,
                    leaseContracts = 3,
                    cashFlowEntries = cashFlows.Count,
                    reminders = reminders.Count,
                    equipments = 4,
                    maintenanceRecords = 2,
                    documents = 3,
                    usagePeriods = 2,
                    saleListings = 1
                },
                assetIds = new [] { asset1.Id, asset2.Id, asset3.Id },
                nextSteps = new[]
                {
                    "GET /api/assets — xem danh sách 3 tài sản",
                    "GET /api/assets/nearby?latitude=10.7769&longitude=106.7009&radiusMeters=5000 — test PostGIS",
                    "GET /api/contracts/expiring?days=30 — phải thấy HĐ Nhà phố Quận 7",
                    "GET /api/reports/income?from=...&to=... — báo cáo thu nhập 6 tháng",
                    "GET /api/reports/tax?year=" + now.Year + " — báo cáo thuế",
                    "GET /api/documents/expiring?withinDays=30 — phải thấy HĐ điện lực",
                    "GET /api/reminders/upcoming?days=7 — nhắc lịch sắp tới"
                }
            });
        }

        /// <summary>
        /// DELETE /api/dev/seed
        /// Xoá sạch toàn bộ dữ liệu nghiệp vụ của user đang đăng nhập (không đụng tài khoản).
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> Clear(CancellationToken ct)
        {
            if (!_env.IsDevelopment())
                return NotFound();

            var userId = _currentUser.UserId;

            var assets = await _db.Assets.Where(a => a.UserId == userId).ToListAsync(ct);
            var reminders = await _db.Reminders.Where(r => r.UserId == userId).ToListAsync(ct);
            var cashFlows = await _db.CashFlowEntries.Where(c => c.UserId == userId).ToListAsync(ct);

            _db.CashFlowEntries.RemoveRange(cashFlows);
            _db.Reminders.RemoveRange(reminders);
            _db.Assets.RemoveRange(assets);
            await _db.SaveChangesAsync(ct);

            // Contact xoá sau cùng vì FK Restrict từ LeaseContract
            var contacts = await _db.ContactParties.Where(c => c.UserId == userId).ToListAsync(ct);
            _db.ContactParties.RemoveRange(contacts);
            await _db.SaveChangesAsync(ct);

            return Ok(new
            {
                message = "Đã xoá sạch dữ liệu nghiệp vụ của tài khoản.",
                removed = new { assets = assets.Count, contacts = contacts.Count, reminders = reminders.Count, cashFlows = cashFlows.Count }
            });
        }

        // ---------- Helpers ----------
        private Point Point(double longitude, double latitude)
            => _geometryFactory.CreatePoint(new Coordinate(longitude, latitude));   // X=lng, Y=lat

        private static DateTime NextMonthDay(DateTime from, int day)
        {
            var next = from.AddMonths(1);
            return new DateTime(next.Year, next.Month,
                Math.Min(day, DateTime.DaysInMonth(next.Year, next.Month)), 0, 0, 0, DateTimeKind.Utc);
        }
    }
}
