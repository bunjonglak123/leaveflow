import { passwordProblem } from './password-rules';

describe('passwordProblem', () => {
  it('asks for a new password when empty', () => {
    expect(passwordProblem('', '')).toBe('กรอกรหัสผ่านใหม่');
  });

  it('rejects fewer than 8 characters', () => {
    expect(passwordProblem('abc1234', 'abc1234')).toBe('รหัสผ่านต้องมีอย่างน้อย 8 ตัวอักษร');
  });

  it('rejects a confirmation that does not match', () => {
    expect(passwordProblem('brandnew99', 'brandnew98')).toBe('ยืนยันรหัสผ่านไม่ตรงกัน');
  });

  it('accepts a valid matching password', () => {
    expect(passwordProblem('brandnew99', 'brandnew99')).toBe('');
  });

  it('skips the confirmation check when no confirmation is asked for', () => {
    expect(passwordProblem('fromhr1234')).toBe('');
    expect(passwordProblem('short')).toBe('รหัสผ่านต้องมีอย่างน้อย 8 ตัวอักษร');
  });
});
