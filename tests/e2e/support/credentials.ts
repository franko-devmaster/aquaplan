/** Test user credentials mapped by role */
export const TEST_USERS: Record<string, { email: string; password: string }> = {
  'administrateur': { email: 'admin@aquaplan.ch', password: 'Admin123!' },
  'admin': { email: 'admin@aquaplan.ch', password: 'Admin123!' },
  'mandataire SIE': { email: 'm.dupont@sie-fribourg.ch', password: 'Test1234!' },
  'mandataire Gruyere': { email: 'a.martin@gruyere-energie.ch', password: 'Test1234!' },
  'mandataire Glane': { email: 'p.favre@commune-romont.ch', password: 'Test1234!' },
  'preleveur': { email: 'j.schneider@labo-fribourg.ch', password: 'Test1234!' },
  'lecteur': { email: 'c.mueller@fr.ch', password: 'Test1234!' },
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
