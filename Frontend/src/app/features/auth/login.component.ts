import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  email = '';
  password = '';
  isLoading = false;
  errorMessage = '';
  async onSubmit(): Promise<void> {
    if (this.isLoading) return;
    this.isLoading = true;
    this.errorMessage = '';
    try {
      if (await this.authService.login(this.email, '')) {
        const target = this.route.snapshot.queryParamMap.get('returnUrl');
        await this.router.navigateByUrl(target === '/documents' ? target : '/chat');
      } else {
        this.errorMessage = 'Enter a valid email address to try the member preview.';
      }
    } catch {
      this.errorMessage = 'Could not start the demo session. Please try again.';
    } finally {
      this.isLoading = false;
    }
  }
  continueAsGuest(): void {
    this.authService.logout();
    this.router.navigate(['/chat']);
  }
}
