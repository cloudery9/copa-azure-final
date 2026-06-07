// =============================================================================
// Story 2.3 / F3 — Identidade v2 com MSAL.js (Authorization Code Flow + PKCE).
//
// ADE-005 Invariante 2 (caminho b — RECOMENDADO): App Registration tipo SPA no
// tenant Entra workforce; @azure/msal-browser obtém o access token e o front o
// envia como `Authorization: Bearer <token>` ao gateway YARP, que valida o JWT.
//
// O fluxo v1 (bcrypt + JWT HS256 local, em src/lib/api.ts) permanece INTOCADO —
// este módulo é paralelo, para comparação didática v1 vs v2.
//
// Sem client secret no browser: PKCE protege o code exchange (SPA público).
// Valores de configuração vêm de variáveis Vite (VITE_ENTRA_*) — nunca hardcoded
// (ADE-005 Inv 5 / AC-14).
//
// Anti-hallucination (AC-14): APIs PublicClientApplication, loginPopup,
// acquireTokenSilent, acquireTokenPopup e os tipos Configuration/RedirectRequest
// validados contra docs oficiais @azure/msal-browser (msaljs).
// =============================================================================

import {
  PublicClientApplication,
  type Configuration,
  type AccountInfo,
  type RedirectRequest,
  InteractionRequiredAuthError,
} from '@azure/msal-browser';

const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID ?? '';
const tenantId = import.meta.env.VITE_ENTRA_TENANT_ID ?? '';
// Authority do tenant workforce (NÃO 'common' — alinhado ao gateway fail-closed AC-6).
const authority = tenantId
  ? `https://login.microsoftonline.com/${tenantId}`
  : 'https://login.microsoftonline.com/organizations';
const redirectUri = import.meta.env.VITE_ENTRA_REDIRECT_URI ?? window.location.origin;

// Scope da API exposta pela App Registration (ex.: api://<client-id>/purchase.write).
// Fallback para o formato padrão se VITE_ENTRA_SCOPE não estiver definido.
const apiScope =
  import.meta.env.VITE_ENTRA_SCOPE ??
  (clientId ? `api://${clientId}/purchase.write` : 'openid');

/** True quando as variáveis mínimas de identidade v2 estão configuradas. */
export const isEntraConfigured = (): boolean =>
  Boolean(clientId && tenantId);

const msalConfig: Configuration = {
  auth: {
    clientId,
    authority,
    redirectUri,
    // Não pede consentimento de novo a cada navegação.
    navigateToLoginRequestUrl: false,
  },
  cache: {
    // sessionStorage: token não persiste entre abas/fechamento — mais seguro p/ SPA.
    cacheLocation: 'sessionStorage',
    storeAuthStateInCookie: false,
  },
};

/**
 * Instância única do MSAL. Deve ser inicializada (await msalInstance.initialize())
 * antes do primeiro uso — feito no bootstrap (main.tsx / MsalProvider).
 */
export const msalInstance = new PublicClientApplication(msalConfig);

/** Scopes solicitados no login v2 (escopo da API + OIDC básico). */
export const loginRequest: RedirectRequest = {
  scopes: [apiScope],
};

/**
 * AC-5 — obtém um access token v2 silenciosamente (acquireTokenSilent); se a
 * sessão exigir interação (token expirado sem refresh, consent), cai para popup.
 * Retorna null se não houver conta logada.
 */
export async function getV2AccessToken(): Promise<string | null> {
  const account: AccountInfo | undefined = msalInstance.getAllAccounts()[0];
  if (!account) {
    return null;
  }

  try {
    const result = await msalInstance.acquireTokenSilent({
      ...loginRequest,
      account,
    });
    return result.accessToken;
  } catch (error) {
    // Token expirado/sem refresh válido → interação explícita (AC-12 cenário didático).
    if (error instanceof InteractionRequiredAuthError) {
      const result = await msalInstance.acquireTokenPopup(loginRequest);
      return result.accessToken;
    }
    throw error;
  }
}
