-- Attendee-based event membership. Replaces "alsaqr-2026".event_members (see event_member.sql),
-- which keyed attendance directly on users; attendance is now keyed on the attendees table so it
-- mirrors group_attendees (see vw_group_attendees.sql / vw_event_attendees.sql).

create table "alsaqr-2026".event_attendees (
  id uuid not null default gen_random_uuid (),
  event_id uuid not null,
  created_at timestamp with time zone not null default now(),
  attendee_id uuid not null,
  group_id uuid not null,
  is_event_organizer boolean null default false,
  constraint event_attendees_pkey primary key (id),
  constraint event_attendees_attendee_id_fkey foreign KEY (attendee_id) references "alsaqr-2026".attendees (id) on update CASCADE on delete CASCADE,
  constraint event_attendees_event_id_fkey foreign KEY (event_id) references "alsaqr-2026".events (id) on update CASCADE on delete CASCADE,
  constraint event_attendees_group_id_fkey foreign KEY (group_id) references "alsaqr-2026".groups (id) on update CASCADE on delete CASCADE
) TABLESPACE pg_default;
