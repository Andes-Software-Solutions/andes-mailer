/** An RFC 9457 error body, as the API writes every failure. */
export interface ProblemDetails {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  /** A message meant for the person, when the API has one. */
  readonly detail?: string;
  readonly instance?: string;
  /** Validation failures by field name. */
  readonly errors?: Readonly<Record<string, readonly string[]>>;
}
