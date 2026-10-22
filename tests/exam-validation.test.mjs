import test from 'node:test';
import assert from 'node:assert/strict';
import { examSaveIssues } from '../src/lib/examValidation.js';

const valid = { courseId: 1, classId: 1, examName: 'Vấn đáp', startTime: '2026-10-25T08:00', timePerStudent: 15, mainQuestionCount: 2, studentIds: [2] };
test('Missing time and students produce specific guidance instead of a silent disabled button', () => {
  const issues = examSaveIssues({ ...valid, startTime: '', studentIds: [] }, 10);
  assert.ok(issues.some(i => i.includes('ngày và giờ')));
  assert.ok(issues.some(i => i.includes('ít nhất một sinh viên')));
});
test('Insufficient bank reports available/requested counts and how to resolve it', () => {
  const issues = examSaveIssues({ ...valid, mainQuestionCount: 3 }, 1);
  assert.equal(issues.length, 1);
  assert.match(issues[0], /1 câu.*3.*Giảm số câu chính/);
});
test('Valid selection is immediately ready for saving after corrections', () => {
  assert.deepEqual(examSaveIssues(valid, 2), []);
  assert.deepEqual(examSaveIssues({ ...valid, mainQuestionCount: 1 }, 1), []);
});
test('Missing catalog is distinguished from an empty bank', () => {
  assert.match(examSaveIssues(valid, undefined).join(' '), /Chưa tải được/);
  assert.match(examSaveIssues(valid, 0).join(' '), /0 câu/);
});
test('Invalid duration and new exam without a class are rejected; legacy class is optional', () => {
  assert.match(examSaveIssues({ ...valid, timePerStudent: 0 }, 2).join(' '), /1–180/);
  assert.match(examSaveIssues({ ...valid, classId: '' }, 2).join(' '), /lớp học phần/);
  assert.deepEqual(examSaveIssues({ ...valid, classId: '' }, 2, false), []);
});
