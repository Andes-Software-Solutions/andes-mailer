/** The signed-in user, as `GET /api/users/me` returns it. */
export interface CurrentUser {
  readonly id: string;
  readonly name: string;
}
