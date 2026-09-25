import { useState } from "react";
import { useNavigate } from "react-router-dom";
import "../styles/Login.css";

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function Waveform() {
  // Decorative bars evoking a voice waveform — the "viva" / oral-exam motif.
  const heights = [18, 34, 22, 46, 30, 58, 26, 44, 20, 36, 16, 28];
  return (
    <div className="waveform" aria-hidden="true">
      {heights.map((h, i) => (
        <span
          key={i}
          className="waveform__bar"
          style={{ "--h": `${h}px`, "--delay": `${i * 90}ms` }}
        />
      ))}
    </div>
  );
}

function EyeIcon({ open }) {
  return open ? (
    <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true">
      <path
        d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7-10-7-10-7Z"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinejoin="round"
      />
      <circle cx="12" cy="12" r="3.2" fill="none" stroke="currentColor" strokeWidth="1.6" />
    </svg>
  ) : (
    <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true">
      <path
        d="M3 3l18 18M9.9 5.2A10.4 10.4 0 0 1 12 5c6.4 0 10 7 10 7a17.6 17.6 0 0 1-4 4.7M6.2 6.9C3.5 8.8 2 12 2 12s3.6 7 10 7c1.4 0 2.6-.3 3.7-.8M12 9a3 3 0 0 1 3 3"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export default function Login() {
  const [form, setForm] = useState({ email: "", password: "", remember: false });
  const [showPassword, setShowPassword] = useState(false);
  const [errors, setErrors] = useState({});
  const [status, setStatus] = useState("idle"); // idle | submitting | error
  const navigate = useNavigate();

  function updateField(field, value) {
    setForm((prev) => ({ ...prev, [field]: value }));
    if (errors[field]) {
      setErrors((prev) => ({ ...prev, [field]: undefined }));
    }
  }

  function validate() {
    const next = {};
    if (!form.email.trim()) {
      next.email = "Please enter your email.";
    } else if (!EMAIL_PATTERN.test(form.email.trim())) {
      next.email = "Enter a valid email address.";
    }
    if (!form.password) {
      next.password = "Please enter your password.";
    } else if (form.password.length < 6) {
      next.password = "Password must be at least 6 characters.";
    }
    return next;
  }

  async function handleSubmit(event) {
    event.preventDefault();
    const validation = validate();
    setErrors(validation);
    if (Object.keys(validation).length > 0) return;

    setStatus("submitting");
    try {
      // TODO: wire up the real login API in Task 2 (Demo CRUD features).
      await new Promise((resolve) => setTimeout(resolve, 900));
      setStatus("idle");
      navigate("/dashboard");
    } catch {
      setStatus("error");
    }
  }

  return (
    <div className="login">
      <section className="login__stage">
        <div className="login__stage-top">
          <span className="login__mark" aria-hidden="true">
            AIVES
          </span>
          <span className="login__mark-sub">AI-Powered Viva Exam System</span>
        </div>

        <div className="login__stage-body">
          <h1 className="login__headline">
            Every viva
            <br />
            begins with a question.
          </h1>
          <p className="login__lede">
            Sign in to prepare questions, run exams, and grade viva sessions —
            alongside an AI panel that listens as closely as you do.
          </p>
          <Waveform />
        </div>

        <p className="login__stage-foot">
          Question Bank · Exam Management · AI Interview · Report
        </p>
      </section>

      <section className="login__panel">
        <form className="login__form" onSubmit={handleSubmit} noValidate>
          <h2 className="login__form-title">Sign in</h2>
          <p className="login__form-sub">
            Enter your instructor or admin account details.
          </p>

          <div className="field">
            <label htmlFor="email">Email</label>
            <input
              id="email"
              name="email"
              type="email"
              autoComplete="email"
              placeholder="you@university.edu"
              value={form.email}
              onChange={(e) => updateField("email", e.target.value)}
              aria-invalid={Boolean(errors.email)}
              aria-describedby={errors.email ? "email-error" : undefined}
            />
            {errors.email && (
              <span className="field__error" id="email-error">
                {errors.email}
              </span>
            )}
          </div>

          <div className="field">
            <div className="field__label-row">
              <label htmlFor="password">Password</label>
              <a className="field__link" href="#forgot-password">
                Forgot password?
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
                aria-label={showPassword ? "Hide password" : "Show password"}
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

          <label className="checkbox">
            <input
              type="checkbox"
              checked={form.remember}
              onChange={(e) => updateField("remember", e.target.checked)}
            />
            <span>Remember me on this device</span>
          </label>

          {status === "error" && (
            <p className="login__form-error" role="alert">
              Sign in failed. Please check your details and try again.
            </p>
          )}

          <button type="submit" className="submit" disabled={status === "submitting"}>
            {status === "submitting" ? "Signing in…" : "Sign in"}
          </button>

          <div className="divider">
            <span>or</span>
          </div>

          <button type="button" className="sso">
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
            Continue with Google
          </button>

          <p className="login__form-foot">
            Don&apos;t have an account?{" "}
            <a href="#contact-admin">Contact your department admin.</a>
          </p>
        </form>
      </section>
    </div>
  );
}