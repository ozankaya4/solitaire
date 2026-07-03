# Data-protection compliance notes (KVKK / GDPR-shaped)

**Not legal advice.** This documents what the codebase implements and, importantly,
what a lawyer must review before launch.

## What we store

- **Guests:** nothing server-side. Games, progress, statistics, and settings live
  only in the browser (IndexedDB / localStorage). Stated in the privacy policy.
- **Accounts:** username, email, securely hashed password, account creation date;
  server-verified game results (score, time, move count), lifetime statistics, and
  saved games.

## Data-subject rights (implemented)

- **Export** — `GET /api/account/export` (authenticated): returns all stored data
  as a downloadable JSON file (right of access / portability).
- **Delete** — `POST /api/account/delete` (authenticated + anti-forgery + explicit
  username confirmation): cascade-deletes leaderboard entries, saved games, stats,
  then the account, and signs out (right to erasure).
- A short **data-processing notice** for the registration screen lives in the
  frontend locales under `register.*` (the account/registration UI is a later
  task and must display it, linking to the full policy).

## Cookies — is a consent banner required? **No.**

Only two cookies exist, both first-party and strictly necessary, set **only after
sign-in**:

| Cookie | Purpose | Necessity |
| ------ | ------- | --------- |
| `solitaire.auth` | keeps the user signed in | strictly necessary (functional) |
| `solitaire.csrf` | anti-forgery / CSRF protection | strictly necessary (security) |

There are no advertising, analytics, or tracking cookies. Under the GDPR/ePrivacy
regime and KVKK guidance, strictly-necessary cookies for a service the user
explicitly requested do **not** require prior consent — so no cookie-consent
banner is needed. Guests receive no cookies at all. Settings/game data use
localStorage/IndexedDB for the game the user is actively using (also arguably
strictly necessary). We still disclose all of this in the policy.

## Needs a lawyer's review (placeholders in the policy)

The policy text intentionally contains bracketed placeholders that must be
resolved before publication:

1. **Data controller identity + contact** — a real legal entity/person and contact
   address (KVKK "veri sorumlusu" registration/VERBİS obligations may apply).
2. **Exact legal bases** — the KVKK Art. 5 grounds and GDPR Art. 6 grounds stated
   are a reasonable draft, not verified.
3. **Hosting region + cross-border transfer basis** — once hosting is chosen, the
   KVKK explicit-consent / adequacy path and any GDPR transfer safeguards (SCCs
   etc.) must be assessed.
4. **Children / age threshold** — the minimum age and any parental-consent handling
   for the target markets.
5. **"Last updated" date** — set at publication.
6. Whether a separate **KVKK aydınlatma metni** (information notice) and explicit
   consent flows are required for the registration data processing.

Policy text (both languages) is in `frontend/src/locales/{en,tr}.json` under
`privacy.sections`. The Turkish is a draft pending the owner's review.
