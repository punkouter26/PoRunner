import { defineConfig } from 'vite';

export default defineConfig({
    server: {
        host: '0.0.0.0',
        port: 5173,
        watch: {
            usePolling: true,
        },
        proxy: {
            '/gamehub': {
                target: 'http://localhost:5018',
                ws: true,
                changeOrigin: true
            }
        }
    },
    publicDir: 'public',
});
