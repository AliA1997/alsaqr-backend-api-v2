-- Yumna AI agent subscription tables (schema: alsaqr-2026)

create table if not exists "alsaqr-2026".subscriptions (
    id                  uuid primary key default gen_random_uuid(),
    name                text not null,
    daily_request_limit integer not null default 30,
    created_at          timestamptz not null default now(),
    updated_at          timestamptz,
    deleted_at          timestamptz
);

create table if not exists "alsaqr-2026".subscription_daily_use (
    id                 uuid primary key default gen_random_uuid(),
    user_id            uuid not null references "alsaqr-2026".users (id),
    date               date not null default (now() at time zone 'utc')::date,
    number_of_requests integer not null default 0,
    created_at         timestamptz not null default now(),
    updated_at         timestamptz,
    unique (user_id, date)
);

alter table "alsaqr-2026".users
    add column if not exists subscription_id uuid references "alsaqr-2026".subscriptions (id);
