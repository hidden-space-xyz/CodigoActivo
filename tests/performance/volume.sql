\set households 5000
\set past_events 100
\set attendees_per_activity 40
\set audit_entries 100000
\set diagnostic_entries 20000

begin;

create temporary table household on commit drop as
select n, gen_random_uuid() as id
from generate_series(1, :households) as n;

create function pg_temp.first_name(n bigint) returns text immutable language sql as $$
    select (array['Lucía', 'Hugo', 'Martina', 'Mateo', 'Sofía', 'Leo', 'Julia', 'Pablo', 'Vega'])[1 + n % 9]
$$;

create function pg_temp.last_name(n bigint) returns text immutable language sql as $$
    select (array['García Prieto', 'Fernández Rey', 'López Vidal', 'Martín Soto', 'Díaz Cano'])[1 + n % 5]
$$;

insert into users (
    id, first_name, last_name, email, phone, news_consent, gender, created_at, adult_declared_at,
    user_status_type_id, user_type_id, staff_role, is_privacy_officer, two_factor_method,
    password_failed_attempts, two_factor_failed_attempts
)
select
    id,
    pg_temp.first_name(n),
    pg_temp.last_name(n),
    format('hogar-%s@performance.codigoactivo.test', n),
    format('6%s', 10000000 + n),
    n % 12 = 0,
    (array['Male', 'Female'])[1 + n % 2],
    now() - n * interval '5 hours',
    now() - n * interval '5 hours',
    '766f114c-6168-4be5-89f2-bae2a7a919e4',
    (array['b0df7ac6-1312-412f-9c2a-88e6cdfb6e1c', '1c038ae8-306f-4785-a5f5-b9c25e5cc4aa'])[1 + n % 2]::uuid,
    'None', false, 'Email', 0, 0
from household;

insert into users (
    id, first_name, last_name, parent_id, birth_date, news_consent, gender, created_at,
    guardianship_declared_at, user_status_type_id, user_type_id, staff_role, is_privacy_officer,
    two_factor_method, password_failed_attempts, two_factor_failed_attempts
)
select
    gen_random_uuid(),
    pg_temp.first_name(h.n + child),
    pg_temp.last_name(h.n),
    h.id,
    current_date - (3000 + (h.n * 7 + child) % 2500)::int,
    false,
    (array['Male', 'Female'])[1 + (h.n + child) % 2],
    now() - h.n * interval '5 hours',
    now() - h.n * interval '5 hours',
    '45a26e12-404b-43b8-b7a7-cdf5f0fc1c4d',
    '1c038ae8-306f-4785-a5f5-b9c25e5cc4aa',
    'None', false, 'Email', 0, 0
from household h
cross join lateral generate_series(1, h.n % 3) as child;

create temporary table template on commit drop as
select row_number() over (order by event_starts_at) - 1 as position, id
from events
where event_ends_at < current_date;

create temporary table cloned_event on commit drop as
select
    gen_random_uuid() as id,
    t.id as source_id,
    (select max(event_starts_at) - min(event_starts_at) from events where event_ends_at < current_date) + n * 7 as days
from generate_series(1, :past_events) as n
join template t on t.position = n % (select count(*) from template);

insert into events (
    id, title, subtitle, description, featured, thumbnail_id, event_starts_at, event_ends_at,
    early_signup_starts_at, signup_starts_at, signup_ends_at, created_at, created_by
)
select
    c.id, e.title, e.subtitle, e.description, false, e.thumbnail_id, e.event_starts_at - c.days,
    e.event_ends_at - c.days, e.early_signup_starts_at - c.days * interval '1 day',
    e.signup_starts_at - c.days * interval '1 day', e.signup_ends_at - c.days * interval '1 day',
    e.created_at - c.days * interval '1 day', e.created_by
from cloned_event c
join events e on e.id = c.source_id;

insert into event_categories (event_id, event_category_type_id)
select c.id, s.event_category_type_id
from cloned_event c
join event_categories s on s.event_id = c.source_id;

insert into event_terms_documents (event_id, terms_document_id, is_required, display_order)
select c.id, s.terms_document_id, s.is_required, s.display_order
from cloned_event c
join event_terms_documents s on s.event_id = c.source_id;

create temporary table cloned_activity on commit drop as
select gen_random_uuid() as id, a.id as source_id, c.id as event_id, c.days
from activities a
join cloned_event c on c.source_id = a.event_id;

insert into activities (
    id, title, description, location, event_id, activity_modality_type_id, thumbnail_id,
    activity_starts_at, activity_ends_at, created_at, created_by
)
select
    c.id, a.title, a.description, a.location, c.event_id, a.activity_modality_type_id, a.thumbnail_id,
    a.activity_starts_at - c.days * interval '1 day', a.activity_ends_at - c.days * interval '1 day',
    a.created_at - c.days * interval '1 day', a.created_by
from cloned_activity c
join activities a on a.id = c.source_id;

insert into activity_role_capacities (activity_id, activity_role_type_id, desired_count)
select c.id, s.activity_role_type_id, s.desired_count
from cloned_activity c
join activity_role_capacities s on s.activity_id = c.source_id;

create temporary table attendee on commit drop as
select row_number() over () as n, u.id
from users u
where u.id in (select id from household) or u.parent_id in (select id from household);

select count(*) as people from attendee \gset

insert into assignments (user_id, activity_id, activity_role_type_id, assignment_status_id, created_at)
select
    p.id,
    a.id,
    '03a5613e-a2d2-42da-94d2-b6d63c0f01b5',
    '3c172c13-d238-4f0b-a61b-0a5ffc6a53ba',
    a.created_at
from (
    select row_number() over () as n, a.id, a.created_at
    from activities a
    join events e on e.id = a.event_id
    where e.event_ends_at < current_date
) a
join attendee p on (p.n + a.n * 97) % :people < :attendees_per_activity;

insert into event_ratings (id, event_id, user_id, score, most_liked, suggestions)
select
    gen_random_uuid(),
    rated.event_id,
    rated.user_id,
    3 + abs(hashtext(rated.user_id::text)) % 3,
    'Los talleres',
    'Más sesiones'
from (
    select distinct a.event_id, s.user_id
    from assignments s
    join activities a on a.id = s.activity_id
    join household h on h.id = s.user_id
    where abs(hashtext(s.user_id::text || a.event_id::text)) % 3 = 0
) rated;

create function pg_temp.past_log_days(parent regclass) returns date[] language sql as $$
    select array_agg(day)
    from (
        select to_date(right(inhrelid::regclass::text, 8), 'YYYYMMDD') as day
        from pg_inherits
        where inhparent = parent
    ) partitions
    where day < current_date
$$;

create function pg_temp.log_id(days date[], n bigint) returns uuid language sql as $$
    select uuidv7(days[1 + n % cardinality(days)] + make_interval(secs => n % 80000) - now())
$$;

insert into audit_log_entries (id, occurred_at, actor_type, actor_id, client_ip, action, outcome, trace_id)
select
    log.id,
    uuid_extract_timestamp(log.id),
    'User',
    h.id,
    ('10.5.' || log.n % 256 || '.' || (log.n >> 8) % 256)::inet,
    (array['Login', 'CompleteLogin', 'AssignHousehold', 'UnassignActivity', 'UpdateEvent'])[1 + log.n % 5],
    case when log.n % 20 = 0 then 'Rejected' else 'Succeeded' end,
    md5(log.n::text)
from (
    select n, pg_temp.log_id(days, n) as id
    from pg_temp.past_log_days('audit_log_entries') as days
    cross join generate_series(1, :audit_entries) as n
) log
join household h on h.n = 1 + log.n % :households;

insert into diagnostic_log_entries (id, occurred_at, level, category, message, trace_id)
select
    log.id,
    uuid_extract_timestamp(log.id),
    2 + log.n % 3,
    'CodigoActivo.Infrastructure.Communication.EmailOutboxDeliverer',
    format('Entrega %s del correo %s', log.n % 7, md5(log.n::text)),
    md5(log.n::text)
from (
    select n, pg_temp.log_id(days, n) as id
    from pg_temp.past_log_days('diagnostic_log_entries') as days
    cross join generate_series(1, :diagnostic_entries) as n
) log;

commit;

analyze;
