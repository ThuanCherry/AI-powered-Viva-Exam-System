# Gemini cho AIVES: STT Live, TTS và follow-up

## Cấu hình đang hoạt động

Ứng dụng vẫn là React JSX → ASP.NET Core monolith → MySQL hiện có. Authentication/JWT, lịch, assignment, deadline và hai ngân sách follow-up được giữ ở backend. Không import/reset/migrate database. Không đưa API key vào React hoặc Git.

Provider Speech active đã chuyển từ Azure sang **Gemini**; không còn registration hoặc request Azure trong pipeline đang chạy. Các file interface placeholder cũ không được đăng ký; file/audio helper cũ được giữ để tránh xóa tài liệu/code chưa được duyệt.

| Chức năng | Model mặc định/đã test | API backend gọi |
|---|---|---|
| STT tiếng Việt streaming | `gemini-3.5-transcribe-live` | Gemini Live WebSocket `BidiGenerateContent` |
| Đọc câu hỏi | `gemini-3.8-flash-tts` | REST `/v1beta/interactions` |
| Quyết định/sinh follow-up | `gemini-3.8-flash` (cấu hình `AI:Model`) | REST `models/{model}:generateContent` |

Model IDs trên đã được **kiểm tra bằng models.list với key của project**, không chỉ lấy tên từ tài liệu. Live có `bidiGenerateContent`; hai model còn lại có `generateContent`. TTS 3.8 dùng request Interactions theo hướng dẫn TTS chính thức; không tự đổi sang schema TTS preview cũ.

## Tài liệu chính thức đã đọc

- [Live Transcribe](https://ai.google.dev/gemini-api/docs/live-api/live-transcribe): model chuyên transcription, `TEXT`, `vi-VN`, PCM16 mono 16kHz, interim/final; session tối đa 10 phút. Không dùng model conversational để tự chấm/đổi câu trong luồng STT.
- [Live WebSocket API](https://ai.google.dev/api/live): setup trước khi gửi audio, JSON realtimeInput, audioStreamEnd và xử lý đóng phiên.
- [Google C# SDK Live implementation](https://github.com/googleapis/dotnet-genai/blob/main/Google.GenAI/Live.cs): tham khảo backend header authentication. Triển khai dùng `ClientWebSocket` .NET, không cần cài SDK preview.
- [Gemini TTS](https://ai.google.dev/gemini-api/docs/speech-generation): schema Interactions của TTS 3.8, voice `Kore`, WAV unary output; tiếng Việt được hỗ trợ. Không bọc RIFF lần hai nếu provider đã trả WAV.
- [Gemini generateContent](https://ai.google.dev/api/generate-content), [Models API](https://ai.google.dev/api/models): JSON follow-up và inventory theo key.
- [Rate limits](https://ai.google.dev/gemini-api/docs/rate-limits), [pricing](https://ai.google.dev/gemini-api/docs/pricing), [AI Studio quota](https://aistudio.google.com/usage?timeRange=last-28-days&tab=rate-limit).

## Key nằm ở đâu

Tạo key tại [Google AI Studio](https://aistudio.google.com/apikey). Dùng key của project có quyền ba model ở trên; billing/free tier và quota phụ thuộc project. **Không dán key vào chat/React/.env Vite.** Key người dùng cung cấp cho lần kiểm thử đã được lưu trong **ASP.NET User Secrets trên máy**, không có trong source hoặc báo cáo test.

Từ root repo, cấu hình local:

```powershell
dotnet user-secrets set "AI:Provider" "Gemini" --project backend/AIVES.Api/AIVES.Api.csproj
dotnet user-secrets set "AI:ApiKey" "<KEY_CUA_BAN>" --project backend/AIVES.Api/AIVES.Api.csproj
dotnet user-secrets set "AI:Model" "gemini-3.8-flash" --project backend/AIVES.Api/AIVES.Api.csproj
dotnet user-secrets set "Speech:Provider" "Gemini" --project backend/AIVES.Api/AIVES.Api.csproj
dotnet user-secrets set "Speech:LiveModel" "gemini-3.5-transcribe-live" --project backend/AIVES.Api/AIVES.Api.csproj
dotnet user-secrets set "Speech:TtsModel" "gemini-3.8-flash-tts" --project backend/AIVES.Api/AIVES.Api.csproj
dotnet user-secrets set "Speech:Voice" "Kore" --project backend/AIVES.Api/AIVES.Api.csproj
```

LiveModel/TtsModel/Voice có default tương ứng nếu không đặt. AI:Model vẫn bắt buộc để LLM không tự chuyển model ngoài ý muốn. API key chung là `AI:ApiKey`; không cần Azure key/region hoặc Speech:Key. `Speech:Provider=Azure` cũ sẽ làm Speech NotConfigured, không gọi Azure ngầm.

Deploy bằng environment variables ở **backend**:

```text
AI__Provider=Gemini
AI__ApiKey=<secret>
AI__Model=gemini-3.8-flash
Speech__Provider=Gemini
Speech__LiveModel=gemini-3.5-transcribe-live
Speech__TtsModel=gemini-3.8-flash-tts
Speech__Voice=Kore
Speech__MaxConcurrentStreams=4
Speech__DrainTimeoutSeconds=5
```

Giữ `ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, Cors:Origins đang hoạt động. Environment được ưu tiên hơn User Secrets/JSON; cần bỏ giá trị Azure cũ ở environment nếu có. Khởi động lại backend sau khi đổi model/key. Local UserSecretsId: `AIVES-Workflow-Local`.

## Kiểm tra model và hạn mức trước demo

### Nếu cả ba dịch vụ vẫn báo NotConfigured

Profile local `AIVES.Api` đặt cả `DOTNET_ENVIRONMENT` và `ASPNETCORE_ENVIRONMENT` là `Development` để nạp User Secrets nhất quán. Dừng backend cũ rồi chạy lại `dotnet run --project backend/AIVES.Api --launch-profile AIVES.Api`. Frontend trỏ `http://localhost:5234`; sandbox cổng 5240 chủ động chặn AI.

Development nạp User Secrets với reload; trên Windows, nếu đường dẫn APPDATA của shell không tìm được key, backend kiểm tra kho secrets thuộc Windows UserProfile hiện tại (ngoài repo). Không áp dụng fallback này cho Production/sandbox; environment explicit vẫn ưu tiên. API trạng thái bổ sung `configurationEnvironment` và `missing` (chỉ tên trường thiếu), không trả key. Nhờ đó giao diện cho biết backend đang thiếu trường nào thay vì chỉ NotConfigured chung chung.

Phòng thi không tự gọi TTS khi refresh/reconnect hoặc khi cấu hình chuyển sang Configured. Nhấn **Nghe câu hỏi** để chủ động gọi TTS. Đọc trạng thái cấu hình/nhận sự kiện SignalR không gọi Gemini.

Kiểm tra cấu hình an toàn, không khởi động server, không kết nối MySQL và không gọi AI:

```bash
dotnet run --project backend/AIVES.Api --launch-profile AIVES.Api -- --check-configuration
```

Lệnh chỉ in môi trường, boolean có key và trạng thái/model, không in key. Kiểm tra được cả ba Configured vẫn chưa xác nhận key, quyền model hoặc quota. Nếu API `/api/workflow/providers` của backend đang chạy vẫn khác kết quả lệnh này, kiểm tra biến môi trường trong terminal chạy backend và khởi động lại đúng tiến trình. Sau đó tải lại trang phòng thi.

### Kiểm tra dịch vụ thật (chỉ khi có chủ đích gọi Google)

```powershell
dotnet run --project tools/ProviderProbe/ProviderProbe.csproj -- --models
```

Tool gọi models.list thật, in tên và supportedGenerationMethods của ba model đã chọn, **không in key**. Lecturer cũng có thể gọi `GET /api/workflow/gemini/models` có Bearer token. Nếu model không được liệt kê, chưa kết luận usable; kiểm tra quyền project và thực hiện probe.

Quota là theo project, không theo từng API key. Kiểm tra RPM/TPM/RPD, concurrency của Live, spend limit/billing của chính project trong AI Studio. Không hardcode một bộ quota chung vào code/tài liệu. models.list không cung cấp remaining quota, không chứng minh billing đủ. Lần kiểm thử thực đã thành công với key được cung cấp và không nhận 429, nhưng **chưa truy cập dashboard AI Studio để xác nhận số quota còn lại hoặc tier**.

Backend tự giới hạn 4 Live streams/process (tùy chỉnh 1–64), một stream/candidate/process; đây là giới hạn ứng dụng, **không phải quota Google**. Frame ≤6400 bytes/200ms, bình thường client gửi 3200 bytes/100ms. Backend chặn tốc độ audio vượt thời gian thực (buffer cho phép 2 giây), client dừng khi backlog chưa ACK vượt 3 giây. Nếu scale nhiều backend process, cần triển khai thêm shared stream lease/capacity; deadline/ghi transcript vẫn serialize bằng MySQL hiện có.

## Ba lệnh chạy

MySQL hiện có phải đang hoạt động; môi trường local đã chọn `aives_db`. Không import dump vì dump chứa DROP TABLE.

```powershell
npm ci
dotnet run --project backend/AIVES.Api
npm run dev
```

Hai lệnh cuối chạy ở hai terminal riêng. Mở [React](http://localhost:5173), [health](http://localhost:5234/health). Vite proxy hỗ trợ WebSocket `/hubs`. Khi deploy phải có HTTPS và reverse proxy WebSocket; localhost được phép dùng mic qua secure-context exception của trình duyệt.

## Luồng microphone và transcript

1. Student bắt đầu đúng slot → backend cấp lượt/deadline như trước. TTS chỉ đọc câu hiện tại; client không gửi arbitrary text vào TTS endpoint.
2. Nhấn **Trả lời bằng mic**. React xin quyền mic, mở AudioContext/AudioWorklet, downmix/resample thành PCM16 mono 16kHz. Không cần MediaRecorder container hoặc browser SpeechRecognition. Vì gửi PCM trực tiếp nên không còn phụ thuộc MIME WebM/OGG của Brave/Chrome.
3. React dùng SignalR duplex stream `Transcribe(examId, turnId, audioSubject)` với JWT; backend kiểm tra membership, trạng thái, current turn và thời gian trước khi mở Gemini Live.
4. Backend `ClientWebSocket` gửi setup `models/<LiveModel>`, `generationConfig.responseModalities=["TEXT"]`, `inputAudioTranscription.languageCodes=["vi-VN"]`, mode `VERBATIM`. API key nằm trong header backend→Google, không query string/browser.
5. Chờ `setupComplete` mới cho mic gửi audio. Mỗi frame chuyển thành `realtimeInput.audio` base64 với MIME `audio/pcm;rate=16000`. Google có thể gửi JSON trong frame text hoặc binary; backend hỗ trợ cả hai và giới hạn kích thước.
6. `interimInputTranscription` chỉ hiện chữ tạm. `inputTranscription` là final, được append vào Answer/FollowUpQuestion hiện có dưới khóa transaction exam; log `LiveSegment` chống ghi trùng segment. Chỉ sau DB commit mới trả event final kèm toàn bộ transcript đã lưu. Không lưu raw audio hoặc interim vào DB.
7. **Dừng & chốt transcript** flush frame cuối, gửi audioStreamEnd, chờ tối đa 5 giây (config 1–10) để nhận các final còn lại. Không tự coi chữ tạm là final. Khi còn chữ tạm chưa được xác nhận, UI thông báo để thu lại hoặc bổ sung bằng tay.
8. Nộp câu trả lời dùng cùng API/turnId như trước; Gemini LLM nhận câu chính/rubric/transcript, quyết định có cần follow-up. Hai mức ngân sách và deadline vẫn do engine quyết định. STT/TTS không được tự tạo câu hay tăng ngân sách.

Một stream bị chặn ở min(deadline lượt/phiên, 9 phút), thấp hơn giới hạn phiên 10 phút của Google. Có thể mở stream mới cho phần còn lại nếu lượt còn giờ. Backend kiểm tra current turn mỗi giây trong khi nhận audio, và kiểm tra lại khi lưu final; không giữ khóa MySQL suốt kết nối Live. Các quyền và deadline được kiểm tra lại cả khi client sửa request.

Khi mất mạng, stream mic dừng và device được giải phóng; SignalR tự nối lại nhưng **không tự replay audio** để tránh transcript lặp. Transcript final đã lưu khôi phục qua REST/refresh. Nhấn mic để thu tiếp phần chưa có sau khi kết nối lại. Phần chưa được Google xác nhận có thể cần nói lại. Nhập tay vẫn là fallback rõ ràng; không mất transcript đã nộp/final sau refresh.

TTS trả `audio/wav` để phát qua Audio; UI cache audio câu hiện tại trong bộ nhớ, thử lại được khi autoplay bị chặn, hủy request cũ khi đổi câu, pause khi bật mic để tránh thu ngược giọng AI. Provider HTTP timeout 25 giây, retry tối đa một lần khi 429/5xx; client TTS timeout 30 giây. Live setup timeout 10 giây; client báo chưa ready sau 15 giây. Không reconnect/replay provider ngầm.

## API giữ tương thích

- REST start/resume/answer/schedule/exams: giữ nguyên hợp đồng và auth.
- `GET /api/Exams/{id}/attempt/tts?turnId=...`: cùng route, response đổi từ MP3 sang WAV; React dùng response Blob nên nhận MIME thực.
- `POST /api/Exams/{id}/attempt/transcribe`: route WAV upload cũ vẫn có; adapter dùng Gemini Live để transcribe bản ghi ≤15 giây, không gọi Azure. UI mới dùng SignalR streaming thay vì đợi bản ghi 8 giây.
- Hub `/hubs/exam`: giữ JoinExam/events hiện có, thêm duplex streaming `Transcribe` chỉ cho Student sở hữu lượt.
- `GET /api/workflow/providers`: trả Configured/NotConfigured, speechProvider=Gemini và model IDs, không trả key. Configured chỉ nghĩa đủ cấu hình.
- `GET /api/workflow/gemini/models`: Lecturer, inventory thật và link quota dashboard; không cấp quyền quản trị Google cho React.

## Test live và E2E

Probe độc lập (có thể phát sinh chi phí Google):

```powershell
dotnet run --project tools/ProviderProbe/ProviderProbe.csproj -- --models
dotnet run --project tools/ProviderProbe/ProviderProbe.csproj -- --roundtrip
```

Roundtrip gọi LLM thật, tạo WAV tiếng Việt bằng Gemini TTS, chuyển mẫu về PCM16/16kHz và gửi qua Live WebSocket thật. Audio tổng hợp ở `artifacts/provider-probe.wav`, WAV input chuẩn ở `artifacts/provider-probe-input.wav`. Có thể thay bằng file ≤15 giây của bạn:

```powershell
dotnet run --project tools/ProviderProbe/ProviderProbe.csproj -- --wav artifacts/sample-vi.wav
```

Tests tự động không cần key:

```powershell
dotnet test tests/AIVES.Workflow.Tests/AIVES.Workflow.Tests.csproj --verbosity quiet
npm run test:audio
npm run build
npm run lint
```

E2E thật dùng tài khoản demo mới, backend đang chạy:

```powershell
dotnet run --project tools/WorkflowSmoke/WorkflowSmoke.csproj -- --seed-only
node tools/gemini-e2e.mjs
```

Seed-only thêm fixture DEMO mới (không sửa/xóa dữ liệu cũ); credentials nằm ở `artifacts/demo-credentials.local.json`, gitignored. E2E tạo/công bố kỳ thi, kiểm tra quyền, mở stream SignalR, gửi audio tiếng Việt **tổng hợp**, nhận partial/final, đọc TTS từng câu chính/follow-up, nộp transcript qua LLM, kiểm tra persistence và ngân sách, hoàn tất rồi resume. Báo cáo an toàn `artifacts/gemini-e2e-result.json` không có key/token/transcript. Không gọi đó là kiểm thử microphone vật lý.

Kết quả chạy thực ngày 19/10/2026 (Asia/Ho_Chi_Minh):

- Key được Google chấp nhận; models.list xác nhận ba model trên.
- Probe LLM, Gemini TTS và Live STT tiếng Việt đều PASS.
- E2E REST + SignalR + backend + Google + MySQL: **31 kiểm tra PASS**, **23 interim / 4 final**, **1.080.456 bytes WAV TTS**, phiên hoàn tất với **2 câu chính + 2 follow-up**, mỗi câu tối đa 1 follow-up.
- **25/25 tests .NET PASS**; **7/7 tests audio/client PASS**; frontend build/lint PASS.
- Chưa kiểm chứng microphone vật lý, chất lượng nghe của người dùng và quota còn lại trong AI Studio. Audio source của E2E được ghi rõ là fixture tiếng Việt tổng hợp.

## Demo trên React

Lecturer login bằng credentials demo → tạo kỳ thi, chọn môn/câu/sinh viên, start phù hợp giờ server → lưu nháp/preview/công bố. Student login → lịch cá nhân → phòng chờ → bắt đầu → nghe câu hỏi → cấp mic → nói tiếng Việt → quan sát chữ tạm và final → dừng/chốt → sửa transcript nếu cần → nộp → trả lời follow-up → hoàn tất → refresh xem lịch sử.

Thử mất mạng khi thu, từ chối mic, thiết bị mic bị rút, autoplay bị chặn, nộp lại cùng turnId, đúng/sai giờ và hết deadline. Khi mất kết nối không bấm nộp trước khi kiểm tra transcript final được giữ; phần chữ tạm chưa xác nhận cần nói lại/nhập tay.

## Lỗi cần xử lý

| Dấu hiệu | Kiểm tra |
|---|---|
| NotConfigured | AI:Provider, AI:ApiKey, Speech:Provider và cấu hình environment ưu tiên cao hơn |
| Google 401/403 | Key còn hiệu lực, API restriction, project/region và quyền model |
| 404 / setup failed | Model đúng API/phiên bản, inventory theo key; không dùng TTS preview schema cho TTS 3.8 |
| 429 RESOURCE_EXHAUSTED | Quota/concurrency/billing của project; giảm stream hoặc thử sau. Không retry Live tự động |
| LiveAlreadyOpen / LiveCapacity | Dừng tab mic cũ hoặc chờ slot app; không phải bằng chứng Google quota đã hết |
| LiveDeadline / 409 current turn | Server đã đóng/đổi lượt; refresh, không gửi transcript cũ sang câu mới |
| Mic denied/không có thiết bị | Cho phép mic trên localhost/HTTPS, kiểm tra input device; nhập tay dự phòng |
| Chữ tạm còn lại khi dừng | Google chưa xác nhận đoạn cuối; thu lại hoặc bổ sung bằng tay |
| TTS autoplay blocked | Nhấn Nghe câu hỏi; bản WAV đã tạo được cache trong lượt hiện tại |

Key, raw provider error body và nội dung prompt/rubric không được log/đưa xuống Student. Chưa push/merge Git.

### Nộp câu trả lời không gọi LLM
`AI:EnableFollowUpOnSubmit` trong `backend/AIVES.Api/appsettings.json` đang là `false` (thiếu cũng mặc định tắt). Nút Nộp lưu transcript/câu trả lời gắn với câu hỏi và chuyển sang câu tiếp theo, không gọi LLM tạo câu hỏi đào sâu. STT ghi âm và nút Nghe câu hỏi giữ nguyên. Muốn bật lại hỏi đào sâu sau này, đổi thành `true` và khởi động lại backend. Lỗi provider cũ trong lịch sử vẫn được giữ, không xóa dữ liệu.
