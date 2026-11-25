import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './problem-details.model';

const GENERIC = 'Something went wrong. Try again.';
const OFFLINE = "We couldn't reach Andes Mailer. Check your connection and try again.";

/** The API's message for a failed request, ready to show the person. */
export function problemMessage(error: unknown, fallback = GENERIC): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallback;
  }

  if (error.status === 0) {
    return OFFLINE;
  }

  const problem = asProblem(error.error);
  const firstFieldError = problem?.errors ? Object.values(problem.errors).flat()[0] : undefined;

  return problem?.detail ?? firstFieldError ?? (error.status >= 500 ? GENERIC : fallback);
}

export function problemStatus(error: unknown): number | null {
  return error instanceof HttpErrorResponse ? error.status : null;
}

function asProblem(body: unknown): ProblemDetails | null {
  return typeof body === 'object' && body !== null ? (body as ProblemDetails) : null;
}
