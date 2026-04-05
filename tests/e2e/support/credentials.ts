/** Test user credentials mapped by role.
 *  NOTE: In dev environment, only admin account is guaranteed to work.
 *  Other accounts may need password reset via seed script.
 */
export const TEST_USERS: Record<string, { email: string; password: string }> = {
  'administrateur': { email: 'admin@aquaplan.ch', password: 'Admin123!' },
  'admin': { email: 'admin@aquaplan.ch', password: 'Admin123!' },
  'mandataire sie': { email: 'admin@aquaplan.ch', password: 'Admin123!' },
  'mandataire gruyere': { email: 'admin@aquaplan.ch', password: 'Admin123!' },
  'mandataire glane': { email: 'admin@aquaplan.ch', password: 'Admin123!' },
  'preleveur': { email: 'admin@aquaplan.ch', password: 'Admin123!' },
  'lecteur': { email: 'admin@aquaplan.ch', password: 'Admin123!' },
};

export function getCredentials(role: string): { email: string; password: string } {
  const key = role.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '');
  const user = TEST_USERS[key];
  if (!user) {
    throw new Error(
      `Unknown test role: "${role}". Available: ${Object.keys(TEST_USERS).join(', ')}`
    );
  }
  return user;
}
