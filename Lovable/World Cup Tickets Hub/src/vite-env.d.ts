/// <reference types="vite/client" />

// Story 2.3 / F3 — variáveis de ambiente Vite para o fluxo de identidade v2 (MSAL.js).
// Valores reais vêm do .env (não versionado) ou das App Settings do App Service;
// NUNCA hardcoded no repo (ADE-005 Inv 5).
interface ImportMetaEnv {
  /** Backend v1 (Node/Express) — fluxo de comparação didática (intocado). */
  readonly VITE_API_URL?: string;
  /** Application (client) ID da App Registration SPA no tenant Entra workforce. */
  readonly VITE_ENTRA_CLIENT_ID?: string;
  /** GUID do tenant Entra workforce do aluno. */
  readonly VITE_ENTRA_TENANT_ID?: string;
  /** Scope exposto pela App Registration (ex.: api://<client-id>/purchase.write). */
  readonly VITE_ENTRA_SCOPE?: string;
  /** Base URL do gateway YARP v2 (Container App). Ex.: https://gateway-xy.azurecontainerapps.io */
  readonly VITE_GATEWAY_V2_URL?: string;
  /** Redirect URI registrada na App Registration (dev: http://localhost:5173). */
  readonly VITE_ENTRA_REDIRECT_URI?: string;
  /**
   * Story 2.6 / F6 — base das rotas do serviço FlowEvents EXPOSTAS PELO GATEWAY YARP
   * (ex.: https://gateway-xy.azurecontainerapps.io/flow-events). O gateway é o nó zero:
   * injeta X-Correlation-ID também nas chamadas ao FlowEvents. Inclui /api/flow/** (REST)
   * e /hubs/flow (SignalR). NUNCA hardcoded.
   */
  readonly VITE_FLOW_EVENTS_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
