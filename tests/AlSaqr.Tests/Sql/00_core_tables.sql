-- Test-only stand-ins for production tables whose DDL is not in this repo.
-- Columns mirror the entity mappings in AlSaqr.Data/Entities. Real feature DDL
-- (event_attendees.sql, video_streams.sql, ...) is applied after this file.

create table "alsaqr-2026".users (
  id uuid not null default gen_random_uuid () primary key,
  username character varying not null,
  avatar character varying null,
  created_at timestamp with time zone not null default now()
);

create table "alsaqr-2026".groups (
  id uuid not null default gen_random_uuid () primary key,
  slug character varying null,
  name character varying null,
  description text null,
  images text[] null,
  created_at timestamp with time zone not null default now(),
  hq_city_id uuid null,
  founder_id uuid null references "alsaqr-2026".users (id) on delete cascade
);

create table "alsaqr-2026".events (
  id uuid not null default gen_random_uuid () primary key,
  slug character varying null,
  name character varying null,
  description text null,
  images text[] null,
  group_id uuid null references "alsaqr-2026".groups (id) on delete cascade,
  is_online boolean not null default false,
  last_occurred_at timestamp with time zone null,
  times_occurred integer null,
  created_at timestamp with time zone not null default now(),
  updated_at timestamp with time zone null
);

create table "alsaqr-2026".attendees (
  id uuid not null default gen_random_uuid () primary key,
  name character varying null,
  user_id uuid null references "alsaqr-2026".users (id) on delete cascade,
  created_at timestamp with time zone not null default now()
);
