import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
export default defineConfig({plugins:[react()],server:{proxy:{'/ws':{target:'ws://localhost:5078',ws:true},'/api':'http://localhost:5078'}},build:{outDir:'../wwwroot',emptyOutDir:true}});