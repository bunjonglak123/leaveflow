import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../core/auth';

/** Sends "/" to the signed-in role's home page. */
@Component({ selector: 'app-home-redirect', template: '' })
export class HomeRedirect {
  constructor() {
    void inject(Router).navigateByUrl(inject(AuthService).homeUrl(), { replaceUrl: true });
  }
}
