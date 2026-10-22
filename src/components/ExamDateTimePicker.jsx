import { useId, useState } from 'react';
import '../styles/ExamSchedule.css';
import { suggestExamStart } from '../lib/examSchedule.js';

const todayVN = () => new Date(Date.now() + 7 * 3600000).toISOString().slice(0, 10);
const pad = value => String(value).padStart(2, '0');
const labelDate = value => new Intl.DateTimeFormat('vi-VN', { weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'UTC' }).format(new Date(`${value}T00:00:00Z`));

export default function ExamDateTimePicker({ value, onChange, disabled }) {
  const id = useId();
  const [suggested] = useState(() => suggestExamStart());
  const day = value?.slice(0, 10) || '';
  const hour = value?.slice(11, 13) || suggested.slice(11, 13);
  const minute = value?.slice(14, 16) || suggested.slice(14, 16);
  const [month, setMonth] = useState(() => (day || todayVN()).slice(0, 7));
  const [open, setOpen] = useState(false);
  const [year, monthNumber] = month.split('-').map(Number);
  const offset = (new Date(Date.UTC(year, monthNumber - 1, 1)).getUTCDay() + 6) % 7;
  const days = new Date(Date.UTC(year, monthNumber, 0)).getUTCDate();
  const change = (date, h = hour, m = minute) => onChange(`${date}T${h}:${m}`);
  const moveMonth = delta => {
    const next = new Date(Date.UTC(year, monthNumber - 1 + delta, 1));
    if (next.getUTCFullYear() >= 1000 && next.getUTCFullYear() <= 9999) setMonth(next.toISOString().slice(0, 7));
  };
  const timeMenu = (kind, current, count) => <details className="es-time-menu">
    <summary aria-label={kind === 'hour' ? 'Chọn giờ bắt đầu' : 'Chọn phút bắt đầu'}>{current}<span>{kind === 'hour' ? 'giờ' : 'phút'}⌄</span></summary>
    <div className="es-time-options" role="group" aria-label={kind === 'hour' ? 'Giờ' : 'Phút'}>{Array.from({ length: count }, (_, i) => <button key={i} type="button" aria-pressed={pad(i) === current} disabled={disabled} onClick={e => { change(day || todayVN(), kind === 'hour' ? pad(i) : hour, kind === 'minute' ? pad(i) : minute); e.currentTarget.closest('details').open = false; }}>{pad(i)}</button>)}</div>
  </details>;
  return <section className="es-picker" aria-label="Ngày và giờ bắt đầu thi">
    <div className="es-picker-title"><span>BẮT ĐẦU THI</span><small>Giờ Việt Nam · UTC+7</small></div>
    <div className="es-date-time">
      <button type="button" className="es-date-trigger" disabled={disabled} aria-expanded={open} aria-controls={id} onClick={() => { setMonth((day || todayVN()).slice(0, 7)); setOpen(!open); }}><span aria-hidden="true">▦</span><span><small>Ngày thi</small><strong>{day ? labelDate(day) : 'Chọn ngày bắt đầu'}</strong></span><span aria-hidden="true">⌄</span></button>
      <div className="es-clock">{timeMenu('hour', hour, 24)}<b aria-hidden="true">:</b>{timeMenu('minute', minute, 60)}</div>
    </div>
    {open && !disabled && <div id={id} className="es-calendar">
      <div className="es-calendar-heading"><button type="button" aria-label="Tháng trước" onClick={() => moveMonth(-1)}>‹</button><strong>Tháng {monthNumber}, {year}</strong><button type="button" aria-label="Tháng sau" onClick={() => moveMonth(1)}>›</button></div>
      <div className="es-days">{['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'].map(d => <span className="es-weekday" key={d}>{d}</span>)}{Array.from({ length: offset }, (_, i) => <span key={`blank${i}`} />)}{Array.from({ length: days }, (_, i) => {
        const date = `${month}-${pad(i + 1)}`;
        return <button type="button" key={date} aria-label={labelDate(date)} aria-pressed={day === date} className={date === todayVN() ? 'es-today' : ''} onClick={() => { change(date); setOpen(false); }}>{i + 1}</button>;
      })}</div>
      <button className="es-today-button" type="button" onClick={() => { const date = todayVN(); setMonth(date.slice(0, 7)); change(date); setOpen(false); }}>Chọn hôm nay</button>
    </div>}
    {!disabled && <div className="es-quick-times"><button type="button" className="es-recommend-time" title="Chừa ít nhất 1 phút chuẩn bị" onClick={() => { const next = suggestExamStart(); onChange(next); setMonth(next.slice(0, 7)); setOpen(false); }}>+1 phút</button><span>Giờ thường dùng</span>{['08:00', '09:00', '13:30', '15:00'].map(time => <button type="button" key={time} onClick={() => change(day || todayVN(), ...time.split(':'))}>{time}</button>)}</div>}
  </section>;
}
