
  # Nirmata App Dev

  This is a code bundle for Nirmata App Dev. The original project is available at https://www.figma.com/design/2BOMEOZ3arDhhIuXAM7qTk/Nirmata-App-Dev.

Run `npm i` to install the dependencies.

Run `npm run dev` to start the development server.

## HTTPS development certificate

The frontend dev server runs on `https://localhost:8443` and expects a local ASP.NET development certificate exported into `./.certs/localhost.pfx`.

Create the certificate with:

```bash
dotnet dev-certs https -ep .certs/localhost.pfx -p nirmata-dev --trust
```

Then set `VITE_HTTPS_PFX_PASSPHRASE=nirmata-dev` in `nirmata.frontend/.env.local`.