/// <reference path="./vite.config.d.ts" />

import fs from 'node:fs'
import path from 'node:path'

import { defineConfig, loadEnv } from 'vite'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'

const secureOrigin = 'https://localhost:8443'

function resolveHttpsOptions(env: Record<string, string>) {
  const certPath = env.VITE_HTTPS_PFX_PATH
    ? path.resolve(process.cwd(), env.VITE_HTTPS_PFX_PATH)
    : path.resolve(process.cwd(), '.certs', 'localhost.pfx')

  const passphrase = env.VITE_HTTPS_PFX_PASSPHRASE

  if (!fs.existsSync(certPath)) {
    throw new Error(
      `Missing HTTPS dev certificate at ${certPath}. Run dotnet dev-certs https -ep .certs/localhost.pfx -p nirmata-dev --trust from nirmata.frontend.`,
    )
  }

  if (!passphrase) {
    throw new Error(
      'Missing VITE_HTTPS_PFX_PASSPHRASE. Add it to nirmata.frontend/.env.local so Vite can open the HTTPS development certificate.',
    )
  }

  return {
    pfx: fs.readFileSync(certPath),
    passphrase,
  }
}

export default defineConfig(({ command, mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_')
  const https = command === 'build' ? undefined : resolveHttpsOptions(env)

  return {
    plugins: [
      // The React and Tailwind plugins are both required for Make, even if
      // Tailwind is not being actively used – do not remove them
      react(),
      tailwindcss(),
    ],
    server: {
      host: 'localhost',
      port: 8443,
      strictPort: true,
      ...(https ? { https } : {}),
      origin: secureOrigin,
    },
    preview: {
      host: 'localhost',
      port: 8443,
      strictPort: true,
      ...(https ? { https } : {}),
    },
    resolve: {
      alias: {
        // Alias @ to the src directory
        '@': '/src',
      },
    },

    // File types to support raw imports. Never add .css, .tsx, or .ts files to this.
    assetsInclude: ['**/*.svg', '**/*.csv'],
  }
})
