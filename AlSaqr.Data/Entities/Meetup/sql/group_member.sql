
create table if not exists "alsaqr-2026".group_members (
    id        uuid primary key default gen_random_uuid(),
    group_id  uuid not null references "alsaqr-2026".groups (id),
    user_id   uuid not null references "alsaqr-2026".users (id),
    role      public.member_type not null default 'member'::public.member_type,
    joined_at timestamptz not null default now(),
    unique (group_id, user_id)
);