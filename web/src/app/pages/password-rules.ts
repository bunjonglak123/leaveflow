/** Same minimum as the API (Passwords.MinLength). */
export const MIN_PASSWORD = 8;

/** Thai error message for a new password, or '' when it is fine. Pass `confirm` only when the form asks for it. */
export function passwordProblem(next: string, confirm?: string): string {
  if (!next) return 'กรอกรหัสผ่านใหม่';
  if (next.length < MIN_PASSWORD) return `รหัสผ่านต้องมีอย่างน้อย ${MIN_PASSWORD} ตัวอักษร`;
  if (confirm !== undefined && confirm !== next) return 'ยืนยันรหัสผ่านไม่ตรงกัน';
  return '';
}
