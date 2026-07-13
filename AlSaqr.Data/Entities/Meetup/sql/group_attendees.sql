-- Attendee-based group membership. Replaces "alsaqr-2026".group_members (see group_member.sql),
-- which keyed membership directly on users; membership is now keyed on the attendees table, the
-- same way event_attendees keys event attendance (see event_attendees.sql).

create table "alsaqr-2026".group_attendees (
  id uuid not null default gen_random_uuid (),
  group_id uuid not null,
  created_at timestamp with time zone not null default now(),
  attendee_id uuid not null,
  is_group_organizer boolean null default false,
  constraint group_attendees_pkey primary key (id),
  constraint group_attendees_attendee_id_fkey foreign KEY (attendee_id) references "alsaqr-2026".attendees (id) on update CASCADE on delete CASCADE,
  constraint group_attendees_group_id_fkey foreign KEY (group_id) references "alsaqr-2026".groups (id) on update CASCADE on delete CASCADE
) TABLESPACE pg_default;
