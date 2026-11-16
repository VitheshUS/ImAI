export interface UserProfile {
  id: string;
  name: string;
  email: string;
  avatarUrl?: string;
  role: 'admin' | 'researcher' | 'viewer';
  organization: string;
  token?: string;
}
