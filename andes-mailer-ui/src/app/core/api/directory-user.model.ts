/** A person in the Entra directory, as `GET /api/users/directory` returns them. */
export interface DirectoryUser {
  /** The Entra object id. */
  readonly id: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly fullName: string;
  readonly email: string;
}
