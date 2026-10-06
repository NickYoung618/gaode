import { defineConfig } from 'vite';

export default defineConfig({
  root: 'src',
  publicDir: false,
  build: {
    outDir: '../dist',
    emptyOutDir: true,
    rollupOptions: { input: ['src/pages/login.html', 'src/pages/a.html', 'src/pages/data-view.html'] }
  }
});
