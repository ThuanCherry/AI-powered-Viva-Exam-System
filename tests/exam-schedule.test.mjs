import test from 'node:test';
import assert from 'node:assert/strict';
import { buildExamSchedule, suggestExamStart } from '../src/lib/examSchedule.js';

test('Suggested start reserves a full minute at 03:18, rounded to picker precision', () => {
  assert.equal(suggestExamStart(Date.parse('2026-10-19T03:18:00+07:00')), '2026-10-19T03:19');
  assert.equal(suggestExamStart(Date.parse('2026-10-19T03:18:45+07:00')), '2026-10-19T03:20');
  const schedule = buildExamSchedule('2026-10-19T03:18', 15, [2, 3]);
  assert.equal(schedule.endTime, '2026-10-18T20:48:00.000Z');
  assert.ok(new Date(schedule.endTime) > new Date('2026-10-19T03:18:00+07:00'));
});
test('Suggested start crosses midnight, month and year in Vietnam time', () => {
  assert.equal(suggestExamStart(Date.parse('2027-12-31T23:59:30+07:00')), '2028-01-01T00:01');
  assert.equal(suggestExamStart(Date.parse('2028-02-28T23:59:00+07:00')), '2028-02-29T00:00');
});

test('Preview computes sequential slots across Vietnamese midnight and year end', () => {
  const schedule = buildExamSchedule('2027-12-31T23:50', 20, [3, 2]);
  assert.equal(schedule.endTime, '2027-12-31T17:30:00.000Z');
  assert.deepEqual(schedule.slots.map(s => s.studentId), [3, 2]);
  assert.equal(schedule.slots[0].endTime, schedule.slots[1].startTime);
  assert.equal(schedule.slots[1].endTime, schedule.endTime);
});
test('Preview recalculates after roster, duration and start change', () => {
  assert.equal(buildExamSchedule('2026-10-20T08:00', 15, [2, 3]).endTime, '2026-10-20T01:30:00.000Z');
  assert.equal(buildExamSchedule('2026-10-20T09:00', 25, [3]).endTime, '2026-10-20T02:25:00.000Z');
});
test('Preview crosses a leap day and month with a non-default host timezone', () => {
  assert.equal(buildExamSchedule('2028-02-28T23:50', 20, [2, 3]).endTime, '2028-02-28T17:30:00.000Z');
  assert.equal(buildExamSchedule('2026-10-31T23:50', 20, [2]).endTime, '2026-10-31T17:10:00.000Z');
});
test('Empty or invalid inputs do not generate misleading slots', () => {
  for (const [date, minutes, ids] of [['', 15, [2]], ['invalid', 15, [2]], ['2026-10-20T08:00', 0, [2]], ['2026-10-20T08:00', 181, [2]], ['2026-10-20T08:00', 1.5, [2]], ['2026-10-20T08:00', 15, []]]) {
    assert.deepEqual(buildExamSchedule(date, minutes, ids), { endTime: null, slots: [] });
  }
});
