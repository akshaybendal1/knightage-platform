import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { Auth, SESSION_ENDED_MESSAGE } from './auth';

// Calls that go out exactly as they are: this app's config (loaded before anything else), and Identity calls that sign
// in or look after the session themselves.
const UNTOUCHED = /\/api\/(client-config|auth\/(login|register|google|refresh|logout|forgot-password|reset-password|handoff\/redeem))$/i;

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(Auth);
  if (!auth.isAuthenticated() || UNTOUCHED.test(new URL(req.url, location.origin).pathname)) {
    return next(req);
  }

  const send = (token: string) => next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
  // Renews first when the token is about to run out (see Auth.validToken()).
  return from(auth.validToken()).pipe(
    switchMap((token) =>
      token
        ? send(token).pipe(
            catchError((error: unknown) => {
              // Refused although it looked current (the session was ended, or the server's clock runs ahead):
              // renew once and retry once. A second 401 is passed on as it is.
              if (error instanceof HttpErrorResponse && error.status === 401) {
                return from(auth.tokenAfterRejection(token)).pipe(
                  switchMap((fresh) => (fresh ? send(fresh) : throwError(() => error))),
                );
              }
              return throwError(() => error);
            }),
          )
        : throwError(() => sessionEnded(req)),
    ),
  );
};

/** The request never left: the session is over and the sign-in page is on its way. */
function sessionEnded(req: HttpRequest<unknown>): HttpErrorResponse {
  return new HttpErrorResponse({ status: 401, statusText: 'Session ended', url: req.url, error: { message: SESSION_ENDED_MESSAGE } });
}
