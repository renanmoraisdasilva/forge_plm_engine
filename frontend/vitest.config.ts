import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
    coverage: {
      provider: 'v8',
      // Measure the whole app, not just the modules a test happens to import.
      include: ['src/**/*.{ts,tsx}'],
      exclude: ['src/**/*.test.ts', 'src/vite-env.d.ts'],
      reporter: ['text', 'text-summary', 'html'],
      reportsDirectory: 'coverage',
    },
  },
});
