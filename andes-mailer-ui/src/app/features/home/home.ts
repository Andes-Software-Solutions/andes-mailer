import { NgOptimizedImage } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component } from '@angular/core';
import { CurrentUser } from '@core/api/current-user.model';

@Component({
  imports: [NgOptimizedImage],
  selector: 'app-home',
  styleUrl: './home.scss',
  templateUrl: './home.html',
})
export class Home {
  protected readonly user = httpResource<CurrentUser>(() => '/api/users/me');
}
