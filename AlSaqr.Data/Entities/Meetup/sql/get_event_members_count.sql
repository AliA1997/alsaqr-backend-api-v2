drop function if exists "alsaqr-2026".get_event_members_count(uuid, uuid, varchar);

create or replace function "alsaqr-2026".get_event_members_count(
    p_event_id uuid,
    p_user_id uuid,
    p_search_term varchar default null
)
returns bigint
language plpgsql
as $$
declare
    v_count bigint := 0;
begin
    select count(*)
    into v_count
    from "alsaqr-2026".vw_event_members vem
    where
        vem.event_id = p_event_id
        and vem.user_id <> p_user_id
        and (
            p_search_term is null
            or vem.username ilike '%' || p_search_term || '%'
        );

    return coalesce(v_count, 0);
end;
$$;
