import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RagSettingsService } from '../../core/services/rag-settings.service';
import { AuthService } from '../../core/auth/auth.service';
import { RagSettings } from '../../core/models/rag.model';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss',
})
export class SettingsComponent {
  readonly settingsService = inject(RagSettingsService);
  readonly authService = inject(AuthService);

  settings: RagSettings = { ...this.settingsService.settings() };
  isSavedToastVisible = false;

  // Auth skeleton integration fields
  authProviderType = 'jwt';
  authEndpoint = 'https://api.imai.internal/auth/v1';
  authClientId = 'imai-enterprise-client';

  onSave(): void {
    this.settingsService.updateSettings(this.settings);
    this.isSavedToastVisible = true;
    setTimeout(() => {
      this.isSavedToastVisible = false;
    }, 2500);
  }

  onReset(): void {
    this.settingsService.resetToDefaults();
    this.settings = { ...this.settingsService.settings() };
  }

  toggleAuthEnforcement(): void {
    this.authService.isAuthRequired.update((val) => !val);
  }

  simulateLogout(): void {
    this.authService.logout();
  }

  simulateLogin(): void {
    this.authService.login('alex.morgan@imai.internal', 'password');
  }
}
