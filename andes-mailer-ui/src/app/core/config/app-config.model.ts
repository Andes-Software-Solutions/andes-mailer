/** The Entra application this deployment signs in to. */
export interface AuthConfig {
  readonly clientId: string;
  readonly authority: string;
  readonly apiScopes: readonly string[];
}

/** The Application Insights resource this deployment reports to. */
export interface TelemetryConfig {
  readonly connectionString: string;
}

/** The shape of `public/config.json`, replaced per environment so one build can be promoted. */
export interface AppConfig {
  readonly auth: AuthConfig;
  /** Leave it out and the app sends no telemetry. */
  readonly telemetry?: TelemetryConfig;
}
