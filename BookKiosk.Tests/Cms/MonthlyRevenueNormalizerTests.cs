using System.Collections.Generic;
using System.Linq;
using BookKiosk.CMS.Helpers;
using BookKiosk.CMS.Models.Reports;
using FluentAssertions;
using Xunit;

namespace BookKiosk.Tests.Cms
{
    public class MonthlyRevenueNormalizerTests
    {
        [Fact]
        public void Normalize_WhenItemsAreNull_ReturnsUnavailable()
        {
            var result = MonthlyRevenueNormalizer.Normalize(null, 2026);

            result.Status.Should().Be(MonthlyRevenueValidationStatus.Unavailable);
            result.Rows.Should().BeEmpty();
            result.TotalAnnualRevenue.Should().Be(0m);
            result.TotalAnnualOrders.Should().Be(0);
        }

        [Fact]
        public void Normalize_WhenItemsAreEmpty_ReturnsValidWithTwelveZeroFilledMonths()
        {
            var result = MonthlyRevenueNormalizer.Normalize(new List<MonthlyRevenueItemDto>(), 2026);

            result.Status.Should().Be(MonthlyRevenueValidationStatus.Valid);
            result.Rows.Should().HaveCount(12);
            result.TotalAnnualRevenue.Should().Be(0m);
            result.TotalAnnualOrders.Should().Be(0);

            for (int i = 0; i < 12; i++)
            {
                result.Rows[i].Month.Should().Be(i + 1);
                result.Rows[i].Revenue.Should().Be(0m);
                result.Rows[i].TotalOrders.Should().Be(0);
            }
        }

        [Fact]
        public void Normalize_WhenItemsAreStandardTwelveMonths_ReturnsValidPreservingOrder()
        {
            var items = Enumerable.Range(1, 12).Select(m => new MonthlyRevenueItemDto
            {
                Month = m,
                Revenue = m * 10_000_000m,
                TotalOrders = m * 10
            }).ToList();

            var result = MonthlyRevenueNormalizer.Normalize(items, 2026);

            result.Status.Should().Be(MonthlyRevenueValidationStatus.Valid);
            result.Rows.Should().HaveCount(12);
            result.TotalAnnualRevenue.Should().Be(items.Sum(x => x.Revenue));
            result.TotalAnnualOrders.Should().Be(items.Sum(x => x.TotalOrders));

            for (int i = 0; i < 12; i++)
            {
                result.Rows[i].Month.Should().Be(i + 1);
                result.Rows[i].Revenue.Should().Be((i + 1) * 10_000_000m);
                result.Rows[i].TotalOrders.Should().Be((i + 1) * 10);
            }
        }

        [Fact]
        public void Normalize_WhenItemsAreSparse_ZeroFillsMissingMonths()
        {
            var items = new List<MonthlyRevenueItemDto>
            {
                new() { Month = 3, Revenue = 25_000_000m, TotalOrders = 70 },
                new() { Month = 8, Revenue = 30_000_000m, TotalOrders = 40 }
            };

            var result = MonthlyRevenueNormalizer.Normalize(items, 2026);

            result.Status.Should().Be(MonthlyRevenueValidationStatus.Valid);
            result.Rows.Should().HaveCount(12);
            result.TotalAnnualRevenue.Should().Be(55_000_000m);
            result.TotalAnnualOrders.Should().Be(110);

            result.Rows[0].Month.Should().Be(1);
            result.Rows[0].Revenue.Should().Be(0m);
            result.Rows[0].TotalOrders.Should().Be(0);

            result.Rows[2].Month.Should().Be(3);
            result.Rows[2].Revenue.Should().Be(25_000_000m);
            result.Rows[2].TotalOrders.Should().Be(70);

            result.Rows[7].Month.Should().Be(8);
            result.Rows[7].Revenue.Should().Be(30_000_000m);
            result.Rows[7].TotalOrders.Should().Be(40);
        }

        [Fact]
        public void Normalize_WhenItemsContainDuplicateMonth_RejectsAsInvalidContract()
        {
            // REVENUE-MONTHLY-01 Option A: Trùng lặp tháng 3 không được gộp, phải bị từ chối
            var items = new List<MonthlyRevenueItemDto>
            {
                new() { Month = 1, Revenue = 10_000_000m, TotalOrders = 10 },
                new() { Month = 3, Revenue = 15_000_000m, TotalOrders = 50 },
                new() { Month = 3, Revenue = 25_000_000m, TotalOrders = 70 } // Trùng tháng 3
            };

            var result = MonthlyRevenueNormalizer.Normalize(items, 2026);

            result.Status.Should().Be(MonthlyRevenueValidationStatus.InvalidContract);
            result.ErrorMessage.Should().Contain("trùng lặp");
            result.Rows.Should().BeEmpty();
            result.TotalAnnualRevenue.Should().Be(0m);
            result.TotalAnnualOrders.Should().Be(0);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(13)]
        [InlineData(-1)]
        [InlineData(100)]
        public void Normalize_WhenMonthIsOutOfRange_RejectsAsInvalidContract(int invalidMonth)
        {
            var items = new List<MonthlyRevenueItemDto>
            {
                new() { Month = invalidMonth, Revenue = 10_000_000m, TotalOrders = 10 }
            };

            var result = MonthlyRevenueNormalizer.Normalize(items, 2026);

            result.Status.Should().Be(MonthlyRevenueValidationStatus.InvalidContract);
            result.ErrorMessage.Should().Contain("ngoài khoảng 1..12");
            result.Rows.Should().BeEmpty();
        }

        [Fact]
        public void Normalize_WhenRevenueOrOrdersAreNegative_RejectsAsInvalidContract()
        {
            var negativeRevenueItems = new List<MonthlyRevenueItemDto>
            {
                new() { Month = 2, Revenue = -5_000_000m, TotalOrders = 10 }
            };

            var negRevResult = MonthlyRevenueNormalizer.Normalize(negativeRevenueItems, 2026);
            negRevResult.Status.Should().Be(MonthlyRevenueValidationStatus.InvalidContract);
            negRevResult.ErrorMessage.Should().Contain("âm");

            var negativeOrdersItems = new List<MonthlyRevenueItemDto>
            {
                new() { Month = 2, Revenue = 5_000_000m, TotalOrders = -5 }
            };

            var negOrdResult = MonthlyRevenueNormalizer.Normalize(negativeOrdersItems, 2026);
            negOrdResult.Status.Should().Be(MonthlyRevenueValidationStatus.InvalidContract);
            negOrdResult.ErrorMessage.Should().Contain("âm");
        }
    }
}
