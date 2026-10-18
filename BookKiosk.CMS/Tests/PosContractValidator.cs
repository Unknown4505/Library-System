using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BookKiosk.CMS.Models.Api.Pos;

namespace BookKiosk.CMS.Tests
{
    /// <summary>
    /// Contract Validator: Kiểm tra tính tương thích dữ liệu giữa Golden Fixtures (Backend thật) và DTO CMS
    /// </summary>
    public static class PosContractValidator
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static bool ValidateAll(string fixturesDirectory, out List<string> errors)
        {
            var errorList = new List<string>();

            // 1. Kiểm tra fixture book_barcode_success.json
            ValidateFixture<PosBookDto>(fixturesDirectory, "book_barcode_success.json", errorList, (book, errs) =>
            {
                if (book.BookId <= 0) errs.Add("BookId phải > 0");
                if (string.IsNullOrWhiteSpace(book.Barcode)) errs.Add("Barcode không được rỗng");
                if (string.IsNullOrWhiteSpace(book.Title)) errs.Add("Title không được rỗng");
                if (book.SellingPrice <= 0) errs.Add("SellingPrice phải > 0");
                if (book.AvailableStock < 0) errs.Add("AvailableStock không được âm");
            });

            // 2. Kiểm tra fixture member_lookup_success.json
            ValidateFixture<PosMemberDto>(fixturesDirectory, "member_lookup_success.json", errorList, (member, errs) =>
            {
                if (member.MemberId <= 0) errs.Add("MemberId phải > 0");
                if (string.IsNullOrWhiteSpace(member.PhoneNumber)) errs.Add("PhoneNumber không được rỗng");
                if (string.IsNullOrWhiteSpace(member.FullName)) errs.Add("FullName không được rỗng");
            });

            // 3. Kiểm tra fixture promotions_list.json
            ValidateFixture<List<PosPromotionDto>>(fixturesDirectory, "promotions_list.json", errorList, (promos, errs) =>
            {
                if (promos == null || promos.Count == 0) errs.Add("Danh sách promotions không được rỗng");
                else if (promos[0].PromotionId <= 0) errs.Add("PromotionId phải > 0");
            });

            // 4. Kiểm tra fixture category_list.json
            ValidateFixture<List<PosCategoryDto>>(fixturesDirectory, "category_list.json", errorList, (cats, errs) =>
            {
                if (cats == null || cats.Count == 0) errs.Add("Danh sách categories không được rỗng");
                else if (cats[0].CategoryId <= 0) errs.Add("CategoryId phải > 0");
            });

            errors = errorList;
            return errorList.Count == 0;
        }

        private static void ValidateFixture<T>(string baseDir, string fileName, List<string> errors, Action<T, List<string>> assertAction)
        {
            try
            {
                var filePath = Path.Combine(baseDir, fileName);
                if (!File.Exists(filePath))
                {
                    errors.Add($"Không tìm thấy file fixture: {fileName}");
                    return;
                }

                var json = File.ReadAllText(filePath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var dataEl))
                {
                    var data = JsonSerializer.Deserialize<T>(dataEl.GetRawText(), _jsonOptions);
                    if (data == null)
                    {
                        errors.Add($"Deserialize {fileName} trả về null");
                        return;
                    }
                    assertAction(data, errors);
                }
                else
                {
                    errors.Add($"Fixture {fileName} thiếu trường 'data'");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Lỗi khi kiểm tra contract {fileName}: {ex.Message}");
            }
        }
    }
}
