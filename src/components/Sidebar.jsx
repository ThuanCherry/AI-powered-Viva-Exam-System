import { NavLink } from "react-router-dom";
import "../styles/Sidebar.css";

const NAV_ITEMS = [
  { to: "/dashboard", label: "Dashboard", icon: IconGrid },
  { to: "/questions", label: "Question Bank", icon: IconBook },
  { to: "/exams", label: "Exam Management", icon: IconCalendar },
  { to: "/viva", label: "Viva Exam", icon: IconMic },
  { to: "/ai-interview", label: "AI Interview", icon: IconSpark },
  { to: "/reports", label: "Reports", icon: IconBars },
];

function IconGrid() {
  return (
    <svg viewBox="0 0 20 20" width="18" height="18" aria-hidden="true">
      <rect x="2.5" y="2.5" width="6" height="6" rx="1" fill="none" stroke="currentColor" strokeWidth="1.5" />
      <rect x="11.5" y="2.5" width="6" height="6" rx="1" fill="none" stroke="currentColor" strokeWidth="1.5" />
      <rect x="2.5" y="11.5" width="6" height="6" rx="1" fill="none" stroke="currentColor" strokeWidth="1.5" />
      <rect x="11.5" y="11.5" width="6" height="6" rx="1" fill="none" stroke="currentColor" strokeWidth="1.5" />
    </svg>
  );
}

function IconBook() {
  return (
    <svg viewBox="0 0 20 20" width="18" height="18" aria-hidden="true">
      <path
        d="M3 4.2c0-.7.6-1.2 1.2-1.2H10v14H4.2c-.7 0-1.2-.5-1.2-1.2V4.2Z"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinejoin="round"
      />
      <path
        d="M17 4.2c0-.7-.6-1.2-1.2-1.2H10v14h5.8c.7 0 1.2-.5 1.2-1.2V4.2Z"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function IconCalendar() {
  return (
    <svg viewBox="0 0 20 20" width="18" height="18" aria-hidden="true">
      <rect x="2.5" y="3.8" width="15" height="13.2" rx="1.4" fill="none" stroke="currentColor" strokeWidth="1.5" />
      <path d="M2.5 7.6h15M6.2 2.2v3M13.8 2.2v3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
    </svg>
  );
}

function IconMic() {
  return (
    <svg viewBox="0 0 20 20" width="18" height="18" aria-hidden="true">
      <rect x="7.2" y="2.5" width="5.6" height="9.5" rx="2.8" fill="none" stroke="currentColor" strokeWidth="1.5" />
      <path
        d="M4.5 9.8a5.5 5.5 0 0 0 11 0M10 15.3v2.4M7.5 17.7h5"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
      />
    </svg>
  );
}

function IconSpark() {
  return (
    <svg viewBox="0 0 20 20" width="18" height="18" aria-hidden="true">
      <path
        d="M10 2.5c.5 3 2 4.5 5 5-3 .5-4.5 2-5 5-.5-3-2-4.5-5-5 3-.5 4.5-2 5-5Z"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.4"
        strokeLinejoin="round"
      />
      <path
        d="M15.5 13.5c.25 1.4.9 2.1 2.3 2.3-1.4.25-2.05.9-2.3 2.3-.25-1.4-.9-2.05-2.3-2.3 1.4-.25 2.05-.9 2.3-2.3Z"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.2"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function IconBars() {
  return (
    <svg viewBox="0 0 20 20" width="18" height="18" aria-hidden="true">
      <path
        d="M4 17V10M10 17V3M16 17v-6"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
      />
    </svg>
  );
}

function Sidebar() {
  return (
    <aside className="shell-sidebar">
      <div className="shell-sidebar__brand">
        <span className="shell-sidebar__mark">AIVES</span>
        <span className="shell-sidebar__mark-sub">Viva Exam System</span>
      </div>

      <nav className="shell-sidebar__nav" aria-label="Main">
        {NAV_ITEMS.map(({ to, label, icon: Icon }) => (
          <NavLink
            key={to}
            to={to}
            className={({ isActive }) =>
              "shell-sidebar__link" + (isActive ? " is-active" : "")
            }
          >
            <Icon />
            <span>{label}</span>
          </NavLink>
        ))}
      </nav>

      <div className="shell-sidebar__user">
        <span className="shell-sidebar__avatar">TM</span>
        <div className="shell-sidebar__user-info">
          <span className="shell-sidebar__user-name">Dr. Tran Thi Mai</span>
          <span className="shell-sidebar__user-role">Course Instructor</span>
        </div>
        <button type="button" className="shell-sidebar__signout" aria-label="Sign out">
          <svg viewBox="0 0 20 20" width="17" height="17" aria-hidden="true">
            <path
              d="M8 17H4.7c-.7 0-1.2-.5-1.2-1.2V4.2c0-.7.5-1.2 1.2-1.2H8M13 13.5l3.5-3.5-3.5-3.5M16.3 10H8"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.5"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </button>
      </div>
    </aside>
  );
}

export default function AppShell({ title, subtitle, actions, children }) {
  return (
    <div className="shell">
      <Sidebar />
      <div className="shell-main">
        <header className="shell-topbar">
          <div>
            <h1 className="shell-topbar__title">{title}</h1>
            {subtitle && <p className="shell-topbar__subtitle">{subtitle}</p>}
          </div>
          {actions && <div className="shell-topbar__actions">{actions}</div>}
        </header>
        <main className="shell-content">{children}</main>
      </div>
    </div>
  );
}