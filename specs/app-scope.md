# App Scope

Three apps share this backend. These boundaries are authoritative; anything outside them needs an
explicit decision before it is built.

**SocialMedia** — view profiles; follow/unfollow; profile info, posts, and media; like, repost,
and comment on posts; join communities and community discussions; direct messages and
community-discussion messages; lists that can save posts, comments, users, communities,
discussions, and discussion messages. Nearly all of it requires a logged-in user, read from
`UserCacheService`.

**Meetup** — join a group or event; direct-message local guides; events online or in person;
create groups and events for a user.

**Zook** — products across multiple categories; create, update, and delete products for a user.
