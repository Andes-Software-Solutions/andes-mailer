import { roleBadge } from './role-badge';

describe('roleBadge', () => {
  it('shows team administrators as Administrator', () => {
    expect(roleBadge('TeamAdministrator', false)).toEqual({
      label: 'Administrator',
      cssClass: 'badge-admin',
    });
  });

  it('shows team members as Member', () => {
    expect(roleBadge('TeamMember', false)).toEqual({ label: 'Member', cssClass: 'badge-member' });
  });

  it('shows a global administrator as Administrator in any team', () => {
    expect(roleBadge(null, true)?.label).toBe('Administrator');
    expect(roleBadge('TeamMember', true)?.label).toBe('Administrator');
  });

  it('shows nothing without a role', () => {
    expect(roleBadge(null, false)).toBeNull();
  });
});
