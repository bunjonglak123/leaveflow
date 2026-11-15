import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth';

/** Adds the bearer token to API calls and signs out when the API says the session is no longer valid. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token();
  const isApi = req.url.startsWith('/api/');
  const authed = token && isApi ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(authed).pipe(
    catchError((err: unknown) => {
      const isLogin = req.url.startsWith('/api/auth/');
      if (err instanceof HttpErrorResponse && err.status === 401 && isApi && !isLogin && token) auth.logout();
      return throwError(() => err);
    }),
  );
};

/** Thai message from a ProblemDetails error, or a generic fallback. */
export function errorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const title = (err.error as { title?: string } | null)?.title;
    if (title) return title;
    if (err.status === 0) return 'เชื่อมต่อเซิร์ฟเวอร์ไม่ได้';
    if (err.status === 403) return 'คุณไม่มีสิทธิ์ทำรายการนี้';
  }
  return 'เกิดข้อผิดพลาด กรุณาลองใหม่';
}
