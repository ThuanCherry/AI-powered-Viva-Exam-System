import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ApiError } from "../api/client.js";
import { register } from "../api/users.js";
import "../styles/Login.css";

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

// Giới hạn theo RegisterViewModel của backend.
const NAME = { min: 3, max: 100 };
const PASSWORD = { min: 6, max: 100 };

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

function validate(form) {
  const errors = {};
  const name = form.fullName.trim();

  if (!name) errors.fullName = "Please enter your full name.";
  else if (name.length < NAME.min || name.length > NAME.max)
    errors.fullName = `Name must be ${NAME.min}–${NAME.max} characters.`;

  if (!form.email.trim()) errors.email = "Please enter your email.";
  else if (!EMAIL_PATTERN.test(form.email.trim())) errors.email = "Enter a valid email address.";

  if (!form.password) errors.password = "Please choose a password.";
  else if (form.password.length < PASSWORD.min || form.password.length > PASSWORD.max)
    errors.password = `Password must be ${PASSWORD.min}–${PASSWORD.max} characters.`;

  if (!form.confirmPassword) errors.confirmPassword = "Please confirm your password.";
  else if (form.confirmPassword !== form.password)
    errors.confirmPassword = "Passwords do not match.";

  return errors;
}

export default function Register() {
  const navigate = useNavigate();
  const [form, setForm] = useState({ fullName: "", email: "", password: "", confirmPassword: "" });
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [errors, setErrors] = useState({});
  const [status, setStatus] = useState("idle"); // idle | submitting | error
  const [serverError, setServerError] = useState("");

  function updateField(field, value) {
    setForm((prev) => ({ ...prev, [field]: value }));
    if (errors[field]) setErrors((prev) => ({ ...prev, [field]: undefined }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    const validation = validate(form);
    setErrors(validation);
    if (Object.keys(validation).length > 0) return;

    setStatus("submitting");
    setServerError("");
    try {
      await register({
        fullName: form.fullName.trim(),
        email: form.email.trim(),
        password: form.password,
        confirmPassword: form.confirmPassword,
      });
      navigate("/login", { state: { registeredEmail: form.email.trim() } });
    } catch (error) {
      setServerError(
        error instanceof ApiError
          ? error.message
          : "Could not create the account. Please try again."
      );
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
            Join the panel.
            <br />
            Start with a question.
          </h1>
          <p className="login__lede">
            Create an account to build question banks, schedule viva exams,
            and review AI-assisted grading.
          </p>
          <Waveform />
        </div>

        <p className="login__stage-foot">
          Question Bank · Exam Management · AI Interview · Report
        </p>
      </section>

      <section className="login__panel">
        <form className="login__form" onSubmit={handleSubmit} noValidate>
          <h2 className="login__form-title">Create account</h2>
          <p className="login__form-sub">Fill in your details to get started.</p>

          <div className="field">
            <label htmlFor="fullName">Full name</label>
            <input
              id="fullName"
              name="fullName"
              type="text"
              autoComplete="name"
              placeholder="Nguyen Van A"
              value={form.fullName}
              onChange={(e) => updateField("fullName", e.target.value)}
              aria-invalid={Boolean(errors.fullName)}
              aria-describedby={errors.fullName ? "fullName-error" : undefined}
            />
            {errors.fullName && (
              <span className="field__error" id="fullName-error">
                {errors.fullName}
              </span>
            )}
          </div>

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
            <label htmlFor="password">Password</label>
            <div className="field__control">
              <input
                id="password"
                name="password"
                type={showPassword ? "text" : "password"}
                autoComplete="new-password"
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

          <div className="field">
            <label htmlFor="confirmPassword">Confirm password</label>
            <div className="field__control">
              <input
                id="confirmPassword"
                name="confirmPassword"
                type={showConfirm ? "text" : "password"}
                autoComplete="new-password"
                placeholder="••••••••"
                value={form.confirmPassword}
                onChange={(e) => updateField("confirmPassword", e.target.value)}
                aria-invalid={Boolean(errors.confirmPassword)}
                aria-describedby={errors.confirmPassword ? "confirmPassword-error" : undefined}
              />
              <button
                type="button"
                className="field__toggle"
                onClick={() => setShowConfirm((v) => !v)}
                aria-label={showConfirm ? "Hide password" : "Show password"}
                aria-pressed={showConfirm}
              >
                <EyeIcon open={showConfirm} />
              </button>
            </div>
            {errors.confirmPassword && (
              <span className="field__error" id="confirmPassword-error">
                {errors.confirmPassword}
              </span>
            )}
          </div>

          {status === "error" && (
            <p className="login__form-error" role="alert">
              {serverError}
            </p>
          )}

          <button type="submit" className="submit" disabled={status === "submitting"}>
            {status === "submitting" ? "Creating account…" : "Create account"}
          </button>

          <p className="login__form-foot">
            Already have an account? <Link to="/login">Sign in.</Link>
          </p>
        </form>
      </section>
    </div>
  );
}