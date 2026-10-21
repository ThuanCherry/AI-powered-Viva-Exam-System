import { mkdirSync, writeFileSync } from 'node:fs';

// Editable, uncompressed diagrams.net XML. All arrows have explicit source/target.
const out = 'docs/architecture';
mkdirSync(out, { recursive: true });
const cells = [], shapes = [], connections = [];
const escape = value => String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
const byId = new Map();
function box(id, x, y, w, h, fill, stroke, parent = '1', radius = 16) {
  const shape = { id, x, y, w, h, fill, stroke, parent, radius }; byId.set(id, shape); shapes.push(shape);
  cells.push(`<mxCell id="${id}" value="" style="rounded=1;arcSize=8;whiteSpace=wrap;html=0;fillColor=${fill};strokeColor=${stroke};strokeWidth=1.5;" vertex="1" parent="${parent}"><mxGeometry x="${x}" y="${y}" width="${w}" height="${h}" as="geometry"/></mxCell>`);
}
function label(id, value, x, y, w, h, parent = '1', size = 16, color = '#334155', bold = false) {
  const shape = { id, value, x, y, w, h, parent, size, color, bold, text: true }; shapes.push(shape); byId.set(id, shape);
  cells.push(`<mxCell id="${id}" value="${escape(value).replaceAll('\n', '&#xa;')}" style="text;html=0;align=left;verticalAlign=top;whiteSpace=wrap;overflow=hidden;fontFamily=Arial;fontSize=${size};fontColor=${color};fontStyle=${bold ? 1 : 0};spacing=0;" vertex="1" parent="${parent}"><mxGeometry x="${x}" y="${y}" width="${w}" height="${h}" as="geometry"/></mxCell>`);
}
function section(id, title, body, y, height, parent, width, fill = '#ffffff') {
  box(id, 22, y, width - 44, height, fill, '#dbe4ee', parent, 10);
  label(id + '-title', title, 16, 12, width - 80, 24, id, 16, '#0f172a', true);
  label(id + '-body', body, 16, 42, width - 80, height - 48, id, 14);
}
function absolute(shape) {
  if (shape.parent === '1') return shape;
  const parent = absolute(byId.get(shape.parent));
  return { ...shape, x: shape.x + parent.x, y: shape.y + parent.y };
}
function edge(id, source, target, y, request) {
  const a = absolute(byId.get(source)), b = absolute(byId.get(target));
  const forward = a.x < b.x;
  const x1 = forward ? a.x + a.w : a.x, x2 = forward ? b.x : b.x + b.w;
  const color = request ? '#2563eb' : '#0f766e', value = request ? 'Request' : 'Response';
  connections.push({ x1, x2, y, color, value });
  cells.push(`<mxCell id="${id}" value="${value}" style="edgeStyle=orthogonalEdgeStyle;rounded=0;html=0;endArrow=block;endFill=1;startArrow=none;strokeColor=${color};strokeWidth=2;fontColor=${color};fontSize=14;labelBackgroundColor=#ffffff;exitX=${forward ? 1 : 0};exitY=${(y - a.y) / a.h};entryX=${forward ? 0 : 1};entryY=${(y - b.y) / b.h};" edge="1" parent="1" source="${source}" target="${target}"><mxGeometry relative="1" as="geometry"/></mxCell>`);
}

label('title', 'AIVES / KIẾN TRÚC TỔNG THỂ', 48, 32, 1400, 42, '1', 30, '#0f172a', true);
label('subtitle', 'React + ASP.NET Core Monolithic + MySQL  •  Đối chiếu code sau khi tích hợp dev  •  21/10/2026', 50, 83, 1500, 26, '1', 16, '#64748b');

box('frontend', 50, 236, 330, 596, '#eff6ff', '#93c5fd');
label('fe-kicker', '01 / FRONTEND', 22, 22, 286, 25, 'frontend', 14, '#2563eb', true);
label('fe-title', 'React SPA', 22, 56, 286, 38, 'frontend', 27, '#0f172a', true);
label('fe-sub', 'JavaScript JSX · React Router · Vite', 22, 100, 286, 28, 'frontend', 14);
section('fe-pages', 'Giao diện theo vai trò', 'Giảng viên: môn / lớp / sinh viên\nTài liệu / câu hỏi / kỳ thi / lịch thi\nSinh viên: lịch thi / phòng vấn đáp\nAdmin: màn hình có sẵn từ dev', 150, 150, 'frontend', 330);
section('fe-client', 'API & đồng bộ', 'Fetch + JWT Authorization\nSignalR: lịch thi và transcript\nTự kết nối lại / đồng bộ dự phòng', 320, 128, 'frontend', 330);
section('fe-audio', 'Âm thanh trong trình duyệt', 'Microphone → PCM16 / AudioWorklet\nTranscript · nhập tay · phát audio\nKhông chứa Gemini API key', 468, 108, 'frontend', 330);

box('backend', 550, 140, 550, 772, '#f8fafc', '#94a3b8');
label('be-kicker', '02 / BACKEND — MỘT ỨNG DỤNG TRIỂN KHAI', 22, 22, 506, 26, 'backend', 14, '#475569', true);
label('be-title', 'ASP.NET Core Monolith', 22, 58, 506, 40, 'backend', 27, '#0f172a', true);
label('be-sub', 'C# · .NET 10 · REST API + SignalR /hubs/exam', 22, 105, 506, 25, 'backend', 15);
section('be-entry', 'Controllers / Hub / Middleware', 'Users · Courses · Classes · Questions · Exams\nLecturerController · WorkflowController · ExamHub\nJWT / role / ownership · CORS · xử lý lỗi', 153, 128, 'backend', 550);
section('be-business', 'Nghiệp vụ & tác vụ nền', 'AuthService · LecturerService · WorkflowService\nVivaEngine · DocumentStore · ScheduleNotifications\nDeadlineWorker: thời hạn thi / lượt trả lời\nSlot tuần tự · trùng lịch · giữ câu đã phân', 300, 148, 'backend', 550);
section('be-data', 'Data / Models / Integrations', 'AivesDbContext · EF Core 9 · Pomelo MySQL\nGeminiViva · GeminiVivaSpeech · GeminiLiveTranscriber\nHttpClient / ClientWebSocket · cấu hình & key ở server', 467, 126, 'backend', 550);
section('be-files', 'Lưu trữ file riêng trên server', 'App_Data/Documents hoặc Documents:StorageRoot\nPDF / DOCX · tải qua API có kiểm tra quyền\nMySQL lưu metadata; không có dịch vụ S3 trong luồng này', 612, 137, 'backend', 550, '#f1f5f9');

box('database', 1270, 140, 390, 340, '#f0fdfa', '#5eead4');
label('db-kicker', '03 / DATABASE', 22, 22, 346, 25, 'database', 14, '#0f766e', true);
label('db-title', 'MySQL · aives_db', 22, 58, 346, 38, 'database', 25, '#0f172a', true);
label('db-body', 'users / roles\ncourses / academic_classes / class_students\nquestions / rubrics / rubric_criteria\nexam_sessions / exam_viva_settings\nexam_candidates / exam_questions\nanswers / follow_up_questions / exam_logs\ndocuments & các bảng mô hình bổ sung', 22, 116, 346, 174, 'database', 15);
label('db-note', 'Kết nối EF Core / MySqlConnector · SQL / TCP', 22, 307, 346, 23, 'database', 13, '#0f766e');

box('gemini', 1270, 565, 390, 347, '#faf5ff', '#d8b4fe');
label('ai-kicker', '04 / THIRD PARTY', 22, 22, 346, 25, 'gemini', 14, '#7e22ce', true);
label('ai-title', 'Google Gemini API', 22, 58, 346, 38, 'gemini', 25, '#0f172a', true);
label('ai-body', 'Live Transcribe: tiếng Việt, PCM streaming\nTTS: tạo audio đọc câu hỏi\nLLM: quyết định câu hỏi đào sâu\n\nREST HTTPS: generateContent\nWSS: Gemini Live qua backend', 22, 115, 346, 150, 'gemini', 15);
label('ai-note', 'LLM khi Nộp: đang TẮT mặc định\nAI:EnableFollowUpOnSubmit = false\nSTT / TTS chỉ chạy qua thao tác người dùng', 22, 276, 346, 62, 'gemini', 13, '#7e22ce');

edge('fe-request', 'frontend', 'backend', 455, true);
edge('fe-response', 'backend', 'frontend', 516, false);
edge('db-request', 'backend', 'database', 270, true);
edge('db-response', 'database', 'backend', 332, false);
edge('ai-request', 'backend', 'gemini', 704, true);
edge('ai-response', 'gemini', 'backend', 766, false);
label('http-note', 'HTTP / JSON\nSignalR / WebSocket', 395, 555, 148, 55, '1', 13, '#64748b');
label('sql-note', 'SQL / TCP', 1132, 369, 128, 25, '1', 13, '#64748b');
label('google-note', 'HTTPS / WSS', 1122, 803, 145, 25, '1', 13, '#64748b');

box('footer', 50, 948, 1610, 112, '#f8fafc', '#e2e8f0', '1', 12);
label('foot-title', 'QUY ƯỚC & PHẠM VI', 20, 16, 1570, 24, 'footer', 14, '#475569', true);
label('foot-text', 'Chỉ hai loại mũi tên: Request (xanh dương) và Response (xanh lá). SignalR Response bao gồm sự kiện server push / transcript.\nFrontend chỉ kết nối backend; không truy cập trực tiếp MySQL hoặc Gemini. Các khối bên trong monolith cùng tiến trình, không phải microservices.\nSơ đồ thể hiện luồng đang nối vào ứng dụng; mã legacy / trang mô phỏng chưa được route không được xem là tích hợp đang hoạt động.', 20, 47, 1570, 63, 'footer', 14, '#64748b');

const xml = `<?xml version="1.0" encoding="UTF-8"?>\n<mxfile host="app.diagrams.net" agent="AIVES architecture generator" version="24.7.17"><diagram id="aives-architecture" name="AIVES — Architecture"><mxGraphModel dx="1740" dy="1100" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1740" pageHeight="1100" math="0" shadow="0"><root><mxCell id="0"/><mxCell id="1" parent="0"/>${cells.join('')}</root></mxGraphModel></diagram></mxfile>`;
writeFileSync(`${out}/AIVES_ARCHITECTURE.drawio`, xml);
let svg = `<svg xmlns="http://www.w3.org/2000/svg" width="1740" height="1100" viewBox="0 0 1740 1100"><defs><marker id="req" markerWidth="10" markerHeight="10" refX="8" refY="5" orient="auto"><path d="M0,1 L9,5 L0,9 Z" fill="#2563eb"/></marker><marker id="res" markerWidth="10" markerHeight="10" refX="8" refY="5" orient="auto"><path d="M0,1 L9,5 L0,9 Z" fill="#0f766e"/></marker></defs><rect width="1740" height="1100" fill="#fff"/>`;
for (const shape of shapes) {
  const s = absolute(shape);
  if (!s.text) svg += `<rect x="${s.x}" y="${s.y}" width="${s.w}" height="${s.h}" rx="${s.radius}" fill="${s.fill}" stroke="${s.stroke}" stroke-width="1.5"/>`;
  else svg += `<text font-family="Arial, sans-serif" font-size="${s.size}" fill="${s.color}" font-weight="${s.bold ? 700 : 400}">${s.value.split('\n').map((line, i) => `<tspan x="${s.x}" y="${s.y + s.size + i * (s.size + 7)}">${escape(line)}</tspan>`).join('')}</text>`;
}
for (const e of connections) {
  svg += `<path d="M${e.x1},${e.y} H${e.x2}" fill="none" stroke="${e.color}" stroke-width="2" marker-end="url(#${e.value === 'Request' ? 'req' : 'res'})"/><rect x="${(e.x1 + e.x2) / 2 - 40}" y="${e.y - 25}" width="80" height="22" fill="#fff"/><text x="${(e.x1 + e.x2) / 2}" y="${e.y - 9}" text-anchor="middle" font-family="Arial" font-size="14" fill="${e.color}">${e.value}</text>`;
}
writeFileSync(`${out}/AIVES_ARCHITECTURE.svg`, svg + '</svg>');
console.log('Generated editable drawio + SVG; 6 directed edges, Request/Response only.');
