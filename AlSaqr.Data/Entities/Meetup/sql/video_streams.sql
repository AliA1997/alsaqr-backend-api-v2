-- AlSaqr.Data/Entities/Meetup/sql/video_streams.sql
-- Live video rooms for ONLINE events (specs/video-streaming-online-events.md).
-- No media is ever stored; participation metadata is retained 90 days (§9).

create table if not exists "alsaqr-2026".video_streams (
  id uuid not null default gen_random_uuid (),
  event_id uuid not null,
  room_name character varying not null,
  host_id uuid not null,
  started_at timestamp with time zone not null default now(),
  ended_at timestamp with time zone null,
  constraint video_streams_pkey primary key (id),
  constraint video_streams_event_id_fkey foreign KEY (event_id)
    references "alsaqr-2026".events (id) on update CASCADE on delete CASCADE,
  constraint video_streams_host_id_fkey foreign KEY (host_id)
    references "alsaqr-2026".users (id) on update CASCADE on delete CASCADE,
  -- Room names are stream-scoped, so they are globally unique by construction.
  -- This is what makes a token from an earlier stream unusable on a later one (§4).
  constraint video_streams_room_name_key unique (room_name),
  -- Target for the composite FK below: keeps video_participants.event_id honest.
  constraint video_streams_id_event_key unique (id, event_id)
) TABLESPACE pg_default;

-- One live stream per event (409 on a second start), enforced in the DB.
create unique INDEX IF not exists video_streams_one_live_per_event
  on "alsaqr-2026".video_streams using btree (event_id)
  TABLESPACE pg_default where (ended_at is null);

-- Reaper scan path: all live streams, for the C11 duration ceiling.
create index IF not exists video_streams_live_lookup
  on "alsaqr-2026".video_streams using btree (started_at)
  TABLESPACE pg_default where (ended_at is null);

create table if not exists "alsaqr-2026".video_participants (
  id uuid not null default gen_random_uuid (),
  event_id uuid not null,
  participant_id uuid not null,
  video_stream_id uuid not null,
  role character varying not null default 'viewer'::character varying,
  camera_enabled boolean not null default false,
  muted boolean not null default true,
  sfu_identity character varying null,
  joined_at timestamp with time zone not null default now(),
  left_at timestamp with time zone null,
  left_reason character varying null,
  last_seen_at timestamp with time zone not null default now(),
  constraint video_participants_pkey primary key (id),
  constraint video_participants_participant_id_fkey foreign KEY (participant_id)
    references "alsaqr-2026".users (id) on update CASCADE on delete CASCADE,
  -- Composite FK: the denormalized event_id MUST match the stream's event_id.
  -- Without this the two columns can silently disagree.
  constraint video_participants_stream_event_fkey foreign KEY (video_stream_id, event_id)
    references "alsaqr-2026".video_streams (id, event_id) on update CASCADE on delete CASCADE,
  constraint video_participants_role_check check (
    (role)::text = any (
      (array[
        'host'::character varying,
        'presenter'::character varying,
        'viewer'::character varying
      ])::text[]
    )
  ),
  constraint video_participants_left_reason_check check (
    left_reason is null or (left_reason)::text = any (
      (array[
        'explicit'::character varying,
        'reaped'::character varying,
        'ended'::character varying,
        'expired'::character varying
      ])::text[]
    )
  ),
  -- left_at and left_reason are set together or not at all.
  constraint video_participants_left_consistency check (
    (left_at is null and left_reason is null)
    or (left_at is not null and left_reason is not null)
  )
) TABLESPACE pg_default;

-- One row per user per stream; rejoin updates the row rather than inserting.
create unique INDEX IF not exists video_participants_stream_user_key
  on "alsaqr-2026".video_participants using btree (video_stream_id, participant_id)
  TABLESPACE pg_default;

create index IF not exists video_participants_live
  on "alsaqr-2026".video_participants using btree (video_stream_id)
  TABLESPACE pg_default where (left_at is null);

-- Reaper scan path (C9): stale live participants across all live streams.
create index IF not exists video_participants_last_seen
  on "alsaqr-2026".video_participants using btree (last_seen_at)
  TABLESPACE pg_default where (left_at is null);

-- Retention purge path (§9).
create index IF not exists video_participants_left_at
  on "alsaqr-2026".video_participants using btree (left_at)
  TABLESPACE pg_default where (left_at is not null);
