// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
// Evita registrar un fetch handler no-op que genera warning en consola
// Si no necesitas controlar 'fetch', no registres el listener.
