import { useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { ApiError } from "../api/client.js";
import { login } from "../api/users.js";
import { saveSession } from "../auth/session.js";
import "../styles/Login.css";

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function Waveform() {
  const heights = [16, 32, 22, 48, 28, 56, 36, 48, 24, 38, 18, 30, 44, 20];
  return (
    <div className="waveform" aria-hidden="true">
      {heights.map((h, i) => (
        <span
          key={i}
          className="waveform__bar"
          style={{ "--h": `${h}px`, "--delay": `${i * 85}ms` }}
        />
      ))}
    </div>
  );
}

function EyeIcon({ open }) {
  return open ? (
    <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
      <path
        d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7-10-7-10-7Z"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinejoin="round"
      />
      <circle cx="12" cy="12" r="3.2" fill="none" stroke="currentColor" strokeWidth="1.8" />
    </svg>
  ) : (
    <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
      <path
        d="M3 3l18 18M9.9 5.2A10.4 10.4 0 0 1 12 5c6.4 0 10 7 10 7a17.6 17.6 0 0 1-4 4.7M6.2 6.9C3.5 8.8 2 12 2 12s3.6 7 10 7c1.4 0 2.6-.3 3.7-.8M12 9a3 3 0 0 1 3 3"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export default function Login() {
  const navigate = useNavigate();
  const location = useLocation();
  const registeredEmail = location.state?.registeredEmail ?? "";

  const [form, setForm] = useState({ email: registeredEmail, password: "", remember: false });
  const [showPassword, setShowPassword] = useState(false);
  const [errors, setErrors] = useState({});
  const [status, setStatus] = useState("idle"); // idle | submitting | error
  const [serverError, setServerError] = useState("");

  function updateField(field, value) {
    setForm((prev) => ({ ...prev, [field]: value }));
    if (errors[field]) {
      setErrors((prev) => ({ ...prev, [field]: undefined }));
    }
  }

  function quickFill(email, password) {
    setForm({ email, password, remember: true });
    setErrors({});
    setServerError("");
  }

  function validate() {
    const next = {};
    if (!form.email.trim()) {
      next.email = "Vui lòng nhập địa chỉ email.";
    } else if (!EMAIL_PATTERN.test(form.email.trim())) {
      next.email = "Định dạng email không hợp lệ.";
    }
    if (!form.password) {
      next.password = "Vui lòng nhập mật khẩu.";
    } else if (form.password.length < 6) {
      next.password = "Mật khẩu tối thiểu 6 ký tự.";
    }
    return next;
  }

  async function handleSubmit(event) {
    event.preventDefault();
    const validation = validate();
    setErrors(validation);
    if (Object.keys(validation).length > 0) return;

    setStatus("submitting");
    setServerError("");
    try {
      const user = await login({
        email: form.email.trim(),
        password: form.password,
        rememberMe: form.remember,
      });
      saveSession(user, form.remember);
      const role = (user?.role || "").toLowerCase();
      if (role === "admin") {
        navigate("/admin");
      } else if (role === "student") {
        navigate("/viva");
      } else {
        navigate("/dashboard");
      }
    } catch (error) {
      setServerError(
        error instanceof ApiError
          ? error.message
          : "Đăng nhập không thành công. Vui lòng kiểm tra lại thông tin."
      );
      setStatus("error");
    }
  }

  return (
    <div className="login">
      {/* Left Showcase Stage */}
      <section className="login__stage">
        <div className="login__stage-top">
          <div className="login__logo-badge">
            <span className="login__mark">AIVES</span>
            <span className="login__mark-tag">AI VIVA EXAM</span>
          </div>
          <span className="login__mark-sub">Hệ thống Thi vấn đáp Trực tuyến</span>
        </div>

        <div className="login__stage-body">
          <div className="login__badge-pill">
            <span className="login__badge-dot" /> Nền tảng Đánh giá Năng lực Thế hệ Mới
          </div>

          <h1 className="login__headline">
            Đổi mới thi vấn đáp
            <br />
            <span className="login__headline-gradient">với Trí tuệ nhân tạo.</span>
          </h1>

          <p className="login__lede">
            Tự động hóa toàn trình kỳ thi vấn đáp: Khởi tạo câu hỏi thông minh từ tài liệu, hội đồng giám khảo AI tương tác giọng nói trực tiếp và chấm điểm minh bạch theo Rubric.
          </p>

          <div className="login__waveform-box">
            <Waveform />
            <span className="login__waveform-caption">🎙️ Nhận diện & phản hồi âm thanh trực tiếp</span>
          </div>

          <div className="login__features-grid">
            <div className="login__feature-item">
              <span className="login__feature-icon">🤖</span>
              <div>
                <strong>Sinh câu hỏi thông minh</strong>
                <p>Trích xuất trọng tâm từ PDF/DOCX theo Bloom Level</p>
              </div>
            </div>
            <div className="login__feature-item">
              <span className="login__feature-icon">🎙️</span>
              <div>
                <strong>STT / TTS Thời gian thực</strong>
                <p>Nhận diện giọng nói chuẩn tiếng Việt & quốc tế</p>
              </div>
            </div>
            <div className="login__feature-item">
              <span className="login__feature-icon">📊</span>
              <div>
                <strong>Chấm điểm chuẩn Rubric</strong>
                <p>Khách quan, minh bạch và bảo toàn lịch sử thi</p>
              </div>
            </div>
          </div>
        </div>

        <div className="login__stage-foot">
          <span>Ngân hàng câu hỏi · Quản lý kỳ thi · Viva Room AI · Báo cáo kết quả</span>
        </div>
      </section>

      {/* Right Login Panel */}
      <section className="login__panel">
        <div className="login__card">
          <form className="login__form" onSubmit={handleSubmit} noValidate>
            <div className="login__form-header">
              <h2 className="login__form-title">Đăng nhập</h2>
              <p className="login__form-sub">Vui lòng nhập tài khoản của bạn để bắt đầu làm việc</p>
            </div>

            {registeredEmail && (
              <p className="login__form-notice" role="status">
                ✓ Tạo tài khoản thành công! Bạn có thể đăng nhập ngay.
              </p>
            )}

            {status === "error" && (
              <div className="login__form-error" role="alert">
                <span>⚠️</span> {serverError}
              </div>
            )}

            {/* Email Field */}
            <div className="field">
              <label htmlFor="email">Email tài khoản</label>
              <div className="field__control">
                <input
                  id="email"
                  name="email"
                  type="email"
                  autoComplete="email"
                  placeholder="name@university.edu.vn"
                  value={form.email}
                  onChange={(e) => updateField("email", e.target.value)}
                  aria-invalid={Boolean(errors.email)}
                  aria-describedby={errors.email ? "email-error" : undefined}
                />
              </div>
              {errors.email && (
                <span className="field__error" id="email-error">
                  {errors.email}
                </span>
              )}
            </div>

            {/* Password Field */}
            <div className="field">
              <div className="field__label-row">
                <label htmlFor="password">Mật khẩu</label>
                <a
                  className="field__link"
                  href="#forgot-password"
                  onClick={(e) => {
                    e.preventDefault();
                    alert("Vui lòng liên hệ Quản trị viên (Admin) để đặt lại mật khẩu.");
                  }}
                >
                  Quên mật khẩu?
                </a>
              </div>
              <div className="field__control">
                <input
                  id="password"
                  name="password"
                  type={showPassword ? "text" : "password"}
                  autoComplete="current-password"
                  placeholder="••••••••"
                  value={form.password}
                  onChange={(e) => updateField("password", e.target.value)}
                  aria-invalid={Boolean(errors.password)}
                  aria-describedby={errors.password ? "password-error" : undefined}
                />
                <button
                  type="button"
                  className="field__toggle"
                  onClick={() => setShowPassword((v) => !v)}
                  aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                  aria-pressed={showPassword}
                >
                  <EyeIcon open={showPassword} />
                </button>
              </div>
              {errors.password && (
                <span className="field__error" id="password-error">
                  {errors.password}
                </span>
              )}
            </div>

            {/* Remember Me */}
            <label className="checkbox">
              <input
                type="checkbox"
                checked={form.remember}
                onChange={(e) => updateField("remember", e.target.checked)}
              />
              <span>Ghi nhớ đăng nhập trên thiết bị này</span>
            </label>

            {/* Submit Button */}
            <button type="submit" className="submit" disabled={status === "submitting"}>
              {status === "submitting" ? "Đang xác thực…" : "Đăng nhập hệ thống →"}
            </button>

            {/* Quick Demo Fill Buttons */}
            <div className="login__quick-fill">
              <span className="login__quick-label">⚡ Chọn tài khoản thử nghiệm nhanh:</span>
              <div className="login__quick-buttons">
                <button
                  type="button"
                  className="login__quick-btn"
                  onClick={() => quickFill("admin@aives.vn", "Admin@123456")}
                >
                  👑 Admin
                </button>
                <button
                  type="button"
                  className="login__quick-btn"
                  onClick={() => quickFill("lecturer@aives.vn", "Lecturer@123456")}
                >
                  👨‍🏫 Giảng viên
                </button>
                <button
                  type="button"
                  className="login__quick-btn"
                  onClick={() => quickFill("student1@aives.vn", "Student@123456")}
                >
                  🎓 Sinh viên
                </button>
              </div>
            </div>

            <div className="divider">
              <span>hoặc đăng nhập bằng</span>
            </div>

            {/* SSO */}
            <button
              type="button"
              className="sso"
              onClick={() => alert("Tính năng SSO Google sẽ được tích hợp với Google Workspace của nhà trường.")}
            >
              <svg viewBox="0 0 18 18" width="18" height="18" aria-hidden="true">
                <path
                  fill="#4285F4"
                  d="M17.64 9.2c0-.64-.06-1.25-.16-1.84H9v3.48h4.84a4.14 4.14 0 0 1-1.8 2.72v2.26h2.91c1.7-1.57 2.69-3.88 2.69-6.62Z"
                />
                <path
                  fill="#34A853"
                  d="M9 18c2.43 0 4.47-.8 5.96-2.18l-2.91-2.26c-.81.54-1.84.86-3.05.86-2.35 0-4.34-1.58-5.05-3.71H.98v2.33A9 9 0 0 0 9 18Z"
                />
                <path
                  fill="#FBBC05"
                  d="M3.95 10.71A5.4 5.4 0 0 1 3.67 9c0-.59.1-1.17.28-1.71V4.96H.98A9 9 0 0 0 0 9c0 1.45.35 2.83.98 4.04l2.97-2.33Z"
                />
                <path
                  fill="#EA4335"
                  d="M9 3.58c1.32 0 2.51.46 3.44 1.35l2.58-2.58C13.46.89 11.43 0 9 0A9 9 0 0 0 .98 4.96l2.97 2.33C4.66 5.16 6.65 3.58 9 3.58Z"
                />
              </svg>
              Tài khoản Google trường cấp
            </button>

            <p className="login__form-foot">
              Bạn chưa có tài khoản? <Link to="/register">Đăng ký tài khoản Sinh viên</Link>
            </p>
          </form>
        </div>
      </section>
    </div>
  );
}
