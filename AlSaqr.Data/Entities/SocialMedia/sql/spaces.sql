create table alsaqr -2026.spaces (
  id uuid not null default gen_random_uuid (),
  kind character varying not null,
  community_id uuid null,
  community_discussion_id uuid null,
  title character varying not null,
  host_id uuid not null,
  started_at timestamp with time zone not null default now(),
  ended_at timestamp with time zone null,
  constraint spaces_pkey primary key (id),
  constraint spaces_community_discussion_id_fkey foreign KEY (community_discussion_id) references "alsaqr-2026".community_discussions (id) on update CASCADE on delete CASCADE,
  constraint spaces_community_id_fkey foreign KEY (community_id) references "alsaqr-2026".communities (id) on update CASCADE on delete CASCADE,
  constraint spaces_discussion_kind check (
    (
      (
        ((kind)::text = 'community'::text)
        and (community_discussion_id is null)
      )
      or (
        ((kind)::text = 'community-discussion'::text)
        and (community_discussion_id is not null)
      )
    )
  ),
  constraint spaces_kind_check check (
    (
      (kind)::text = any (
        (
          array[
            'community'::character varying,
            'community-discussion'::character varying
          ]
        )::text[]
      )
    )
  )
) TABLESPACE pg_default;

create unique INDEX IF not exists spaces_one_live_per_community on "alsaqr-2026".spaces using btree (community_id)
where
  (
    (ended_at is null)
    and ((kind)::text = 'community'::text)
  ) TABLESPACE pg_default;

create unique INDEX IF not exists spaces_one_live_per_discussion on "alsaqr-2026".spaces using btree (community_discussion_id)
where
  (
    (ended_at is null)
    and ((kind)::text = 'community-discussion'::text)
  ) TABLESPACE pg_default;

create index IF not exists spaces_live_lookup on "alsaqr-2026".spaces using btree (community_id, community_discussion_id)
where
  (ended_at is null) TABLESPACE pg_default;

create table alsaqr -2026.space_participants (
  id uuid not null default gen_random_uuid (),
  space_id uuid not null,
  user_id uuid not null,
  role character varying not null default 'listener'::character varying,
  muted boolean not null default false,
  hand_raised boolean not null default false,
  sfu_session_id character varying null,
  track_name character varying null,
  sfu_mid character varying null,
  joined_at timestamp with time zone not null default now(),
  left_at timestamp with time zone null,
  last_seen_at timestamp with time zone not null default now(),
  constraint space_participants_pkey primary key (id),
  constraint space_participants_space_id_fkey foreign KEY (space_id) references "alsaqr-2026".spaces (id) on update CASCADE on delete CASCADE,
  constraint space_participants_user_id_fkey foreign KEY (user_id) references "alsaqr-2026".users (id) on update CASCADE on delete CASCADE,
  constraint space_participants_role_check check (
    (
      (role)::text = any (
        (
          array[
            'host'::character varying,
            'speaker'::character varying,
            'listener'::character varying
          ]
        )::text[]
      )
    )
  )
) TABLESPACE pg_default;

create unique INDEX IF not exists space_participants_space_user_key on "alsaqr-2026".space_participants using btree (space_id, user_id) TABLESPACE pg_default;

create index IF not exists space_participants_live on "alsaqr-2026".space_participants using btree (space_id)
where
  (left_at is null) TABLESPACE pg_default;