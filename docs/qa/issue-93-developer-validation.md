# PR #99 / Issue #93 — focused Leader QA remediation

Date: 2026-11-13. Branch: thien1. Integrated main baseline: f241864f20ef5f0b2ec270ad1d697302eb63f118.

This follow-up addresses VAL-BOOKS-READ-01, UPLOAD-RESOURCE-01 and UPLOAD-NAME-01 from the Leader review of caf218db428f20c51adc7f899f14bb524fb87f75. PR #97/thien2 and PR #98/thien3 are unchanged. The working Admin-only attributes, content signature/decode/re-encode pipeline, actual-byte/output 5 MiB limits, 16M pixels, metadata stripping/auto-orientation, static first frame, UUID storage and cleanup are preserved.

## P1 — restore baseline automatic validation

The Books-wide InvalidModelStateResponseFactory added in the first revision is removed. MVC automatic validation for invalid public GET query types and malformed/null Books POST/PUT again produces 400 application/problem+json; charset=utf-8 with type/title/status/errors. No global MVC factory is added. Deliberate controller/service business responses keep their ApiResponseDto contract (for example, duplicate create remains BAD_REQUEST). Existing Users and Barcode BAD_REQUEST wrappers and Orders/Kiosk Checkout ProblemDetails remain unchanged.

The exact seven-case matrix covers page/pageSize/categoryId invalid GET, malformed Books create, malformed Users, malformed Barcode change, and malformed Counter Checkout. Additional cases verify null Books POST/PUT, malformed PUT, malformed/null Kiosk Checkout, and business-vs-framework validation.

## P2 — enforce limits before/at binding and bound decoder work

| Resource | Concrete limit |
|---|---|
| Total upload request | 5 MiB + 64 KiB multipart framing allowance (5,308,416 bytes) |
| Per multipart section | Same 5 MiB + 64 KiB envelope; file validator still enforces exactly 5 MiB |
| Multipart buffering | 64 KiB memory threshold; bounded request caps subsequent temporary buffering |
| Part headers | 8 KiB; at most 16 headers |
| Form values | At most 8; each at most 8 KiB; keys at most 128 characters |
| Concurrent upload requests per API process | 2; queue 0; excess gets safe 429 JSON and Retry-After: 1 |
| ImageMagick pixel cache | At most 256 MiB process-wide; native single memory request at most 128 MiB |
| ImageMagick profiles / threads / image list | At most 1 MiB profile, 2 threads, 32 list entries |
| ImageMagick disk pixel cache | Disabled (Disk = 0) |

The named endpoint concurrency limiter executes after authorization and before MVC model binding, holding its permit through the response. Other endpoints are not rate limited. Native limits are initialized once and do not raise a stricter existing policy. They bound ImageMagick resources, not the entire process RSS. No package/schema/global role change is made in this remediation.

Real Kestrel tests verify an oversized multipart request is rejected before file/SQL mutation. MVC reports this body-size failure as safe 400 ProblemDetails; the test uses Expect: 100-continue to receive the early refusal without a client broken-pipe race. A valid upload immediately afterwards works. A deterministic test holds two tiny multipart requests before binding completes using existing MVC diagnostic events, verifies the third receives 429 while public GET works, then releases both and verifies two unique files. No load/destructive test is used.

## P3 — filename policy A

Reject original filenames containing slash, backslash, colon, consecutive dots or control characters. Valid original basenames still get replaced by a UUID. Prior acceptance of ../../name.jpg did not demonstrate a storage traversal exploit; this change tightens the negative-input contract requested by Leader. Tests retain ordinary JPG/JPEG/PNG/WebP positives and now explicitly reject path-like names.

## Validation and limitations

Clean solution Debug/Release builds: 0 errors, 111 existing warnings each, including existing AutoMapper/PKCS advisories. Targeted Debug: 24/24 PASS. Full Release developer suite: 236/236 PASS, 0 failed, 0 skipped. Test evidence includes the seven-case status/media-type/JSON matrix, JWT 401/403/Admin, SQL/file snapshots, real Kestrel multipart/concurrency, animation first-frame preservation and a valid WebP container at 5 MiB minus 2 bytes (RIFF word alignment, legal JUNK padding) that is decoded and re-encoded.

Pre-cancelled request and a temporary file obstructing upload-directory creation are tested safely; error JSON does not expose the server path, no cover is left, and the next upload recovers. These are not proof of a true mid-write interruption or real ACL denial. True mid-write interruption, real ACL denial, specially corrupted ICC/EXIF and physical CMS/Kiosk/WPF E2E remain NOT RUN. No production injection seam, new test project, harness or factory is added. Existing BooksApiTestHost is unchanged.

All SQL tests opt into a fresh generated disposable database, never the application database. Their fixture verifies database drop; HTTP hosts verify temporary-folder removal. Exact new HEAD, post-commit test results, combined integration snapshot with unchanged #97/#98 and final zero-leftover checks are recorded in the PR handoff/evidence manifest. Developer evidence does not replace Leader independent acceptance; keep #93 and #99 open pending independent retest. The Leader-only D:/BookKiosk_Tests suite is not available on this machine and was excluded as instructed by the user.

Reproduce with BOOKKIOSK_QA_SQL_MASTER pointing to an approved disposable SQL Server master connection:

```powershell
dotnet clean BookKiosk.slnx -c Debug
dotnet build BookKiosk.slnx -c Debug
dotnet test BookKiosk.Tests -c Debug --filter FullyQualifiedName~BooksUploadSecurityTests
dotnet clean BookKiosk.slnx -c Release
dotnet build BookKiosk.slnx -c Release
dotnet test BookKiosk.Tests -c Release
```
