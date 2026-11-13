# Issue #93 — Books upload content validation and HTTP security QA

Branch: `thien1`. Baseline: `main@f241864f20ef5f0b2ec270ad1d697302eb63f118`.

Existing Admin-only attributes are retained. The upload endpoint now verifies the extension/MIME pair, JPEG/PNG/WebP signature and container terminator/length, and actual decode before creating a file. Both declared length and bytes read are bounded to 5 MiB. Image dimensions are inspected before pixel decode, with a 16-million-pixel cap. The server publishes a re-encoded static cover, auto-oriented before removing profiles/metadata; animated input becomes its first frame. Output must also fit within 5 MiB.

The forced allowlisted decoder prevents switching to other ImageMagick formats/delegates. Magick.NET-Q8-AnyCPU 15.1.0 packages the codec runtime; no external ImageMagick installation or license key is required. Dependency/license notices: https://github.com/dlemstra/Magick.NET. Runtime testing was on Windows; Linux deployment was not exercised.

Uploads still use multipart field `file`, return `ApiResponseDto<string>` with `/uploads/books/{UUID}.{extension}`, and keep JPG/JPEG/PNG/WebP support. Files are written under IWebHostEnvironment.WebRootPath using CreateNew after validation; interrupted writes remove the newly created file. Rejected content never creates a cover file. Requests rejected by authorization never change SQL or upload files.

An HTTP test exposed that malformed/null Books JSON was still handled by MVC's default ProblemDetails response. A Books-only ApiBehaviorOptions wrapper now returns safe VALIDATION_ERROR ApiResponseDto; the existing Users/Barcode fallback chain is retained.

| HTTP case | Expected and verified |
|---|---|
| POST / PUT / DELETE / upload, missing JWT | 401, SQL/files unchanged |
| Same four writes, invalid JWT | 401, SQL/files unchanged |
| Same four writes, Staff / Manager JWT | 403, SQL/files unchanged |
| Admin create / update / delete / upload | 201 / 200 / 200 / 200 |
| Public list / active ID / active barcode | 200 |
| Invalid / malformed / null create JSON | 400 ApiResponseDto |
| Delete nonexistent book | 404 |
| Controlled barcode change to occupied barcode | 409 CONFLICT wrapper; SQL unchanged |
| Valid JPG / JPEG / PNG / WebP with traversal filename | 200; UUID basename, decodable stored file and public image URL |
| Empty / >5 MiB / script / magic-only junk / truncated / spoofed extension or MIME / invalid RIFF / appended script | 400 BAD_REQUEST wrapper; zero files |
| >16 million pixels / forged reported length | Rejected; zero files |

Validation (2026-11-13): seven targeted scenarios passed; final full Release suite 219/219 PASS, 0 failed, 0 skipped. Final incremental Debug/Release solution builds: 0 errors, 24 existing warnings each. Existing AutoMapper/PKCS dependency advisories are outside this focused issue and remain visible in build logs. No schema, Auth/Users behavior or global role policy change.

Tests use the real Production API pipeline, signed JWTs and a generated disposable SQL database. Startup seeding is redirected to an isolated InMemory database, never the application SQL database. SQL fixture verifies database removal; host verifies temporary image-directory cleanup. Two aborted decoder experiments were separately cleaned up before final acceptance checks; neither discarded decoder dependency is in the final project.

Reproduce (set BOOKKIOSK_QA_SQL_MASTER to an approved disposable SQL Server master connection):

```powershell
dotnet build BookKiosk.slnx -c Debug
dotnet build BookKiosk.slnx -c Release
dotnet test BookKiosk.Tests -c Debug --filter FullyQualifiedName~BooksUploadSecurityTests
dotnet test BookKiosk.Tests -c Release --filter FullyQualifiedName~BooksUploadSecurityTests
dotnet test BookKiosk.Tests -c Release
```

Exact commit SHA and post-commit targeted results are in the PR. This is developer HTTP/JWT/SQL/filesystem evidence; independent Leader security acceptance and physical CMS/Kiosk E2E remain separate. Keep #93 open for independent review.
