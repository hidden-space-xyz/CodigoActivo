import type { DOMWrapper } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { LeaderRosterPanel } from '@/features/event-leader-roster'
import { i18n } from '@/shared/i18n'
import { formatDate, formatDateTime, formatDateTimeRange } from '@/shared/lib'

import {
  buildLeaderRosterActivity,
  serveLeaderRoster,
} from '../../../support/fixtures/public-dashboard/leader-roster'
import { renderWithProviders, t } from '../../../support/render'

async function renderPanel(signedIn = true) {
  const rendered = await renderWithProviders(LeaderRosterPanel, {
    props: { eventId: 'event-1' },
    ...(signedIn ? { user: {} } : {}),
  })
  await vi.waitFor(() => expect(rendered.wrapper.text()).not.toContain(t('common.loading')))
  return rendered
}

function fields(person: DOMWrapper<Element>): Record<string, string> {
  return Object.fromEntries(
    person.findAll('.lr-field').map((field) => [field.find('dt').text(), field.find('dd').text()]),
  )
}

function link(person: DOMWrapper<Element>, label: string): string | undefined {
  return person
    .findAll('.lr-field')
    .find((field) => field.find('dt').text() === label)
    ?.find('a')
    .attributes('href')
}

describe('LeaderRosterPanel', () => {
  it('lists each led activity with its schedule, place and confirmed attendee count', async () => {
    const requests = serveLeaderRoster([
      buildLeaderRosterActivity(),
      buildLeaderRosterActivity({ activityId: 'act-2', title: 'Charla', location: '', roles: [] }),
    ])

    const { wrapper } = await renderPanel()

    expect(requests).toEqual(['event-1'])
    expect(wrapper.find('[role="note"]').text()).toContain(t('features.leaderRoster.notice.title'))
    const activities = wrapper.findAll('.lr-activity')
    expect(activities.map((activity) => activity.find('h2').text())).toEqual([
      'Taller de robótica',
      'Charla',
    ])
    const first = activities[0]!
    expect(first.find('.lr-activity__meta').text()).toContain(
      formatDateTimeRange('2099-06-10T09:00:00Z', '2099-06-10T11:00:00Z'),
    )
    expect(first.find('.lr-activity__meta').text()).toContain('Aula 3')
    expect(first.find('.lr-activity__count').text()).toBe(
      i18n.global.t('features.leaderRoster.attendeesCount', 6),
    )
    expect(activities[1]!.find('.lr-activity__meta').findAll('span')).toHaveLength(1)
    expect(activities[1]!.find('.lr-activity__count').text()).toBe(
      i18n.global.t('features.leaderRoster.attendeesCount', 0),
    )
  })

  it('groups attendees by role and splits users from dependents', async () => {
    serveLeaderRoster([buildLeaderRosterActivity()])

    const { wrapper } = await renderPanel()

    const roles = wrapper.findAll('.lr-role')
    expect(roles.map((role) => role.find('h3 span').text())).toEqual([
      'Líder',
      'Voluntario',
      'Participante',
    ])
    expect(roles.map((role) => role.find('.lr-role__count').text())).toEqual([
      i18n.global.t('features.leaderRoster.peopleCount', 2),
      i18n.global.t('features.leaderRoster.peopleCount', 1),
      i18n.global.t('features.leaderRoster.peopleCount', 3),
    ])
    expect(roles[0]!.find('.lr-group--dependents').exists()).toBe(false)
    expect(roles[0]!.find('.lr-group--users .lr-group__name').text()).toBe(
      t('features.leaderRoster.users.title'),
    )
    const participants = roles[2]!
    expect(participants.find('.lr-group--users').findAll('.lr-person')).toHaveLength(1)
    expect(participants.find('.lr-group--dependents .lr-group__name').text()).toBe(
      t('features.leaderRoster.dependents.title'),
    )
    expect(participants.find('.lr-group--dependents').findAll('.lr-person')).toHaveLength(2)
  })

  it('shows the agreed fields of an adult attendee with contact links', async () => {
    serveLeaderRoster([buildLeaderRosterActivity()])

    const { wrapper } = await renderPanel()

    const adult = wrapper.findAll('.lr-role')[2]!.find('.lr-group--users .lr-person')
    const columns = 'features.leaderRoster.columns'
    expect(fields(adult)).toEqual({
      [t(`${columns}.firstName`)]: 'Ana',
      [t(`${columns}.lastName`)]: 'Álvarez',
      [t(`${columns}.age`)]: t('features.leaderRoster.adult'),
      [t(`${columns}.email`)]: 'ana@example.test',
      [t(`${columns}.phone`)]: '600 333 444',
      [t(`${columns}.signedUpAt`)]: formatDate('2099-05-04T10:00:00Z'),
    })
    expect(link(adult, t(`${columns}.email`))).toBe('mailto:ana@example.test')
    expect(link(adult, t(`${columns}.phone`))).toBe('tel:600333444')
    const signedUp = adult.find('time')
    expect(signedUp.attributes('datetime')).toBe('2099-05-04T10:00:00Z')
    expect(signedUp.attributes('title')).toBe(formatDateTime('2099-05-04T10:00:00Z'))
  })

  it('shows a dependent with their age and their guardian as the contact', async () => {
    serveLeaderRoster([buildLeaderRosterActivity()])

    const { wrapper } = await renderPanel()

    const [nora, hugo] = wrapper.findAll('.lr-group--dependents .lr-person')
    const columns = 'features.leaderRoster.columns'
    expect(fields(nora!)).toEqual({
      [t(`${columns}.firstName`)]: 'Nora',
      [t(`${columns}.lastName`)]: 'Gil',
      [t(`${columns}.age`)]: i18n.global.t('features.leaderRoster.ageYears', { age: 11 }, 11),
      [t(`${columns}.guardian`)]: 'Gabriela Gil',
      [t(`${columns}.guardianEmail`)]: 'gabriela@example.test',
      [t(`${columns}.guardianPhone`)]: '600 555 666',
      [t(`${columns}.signedUpAt`)]: formatDate('2099-05-05T10:00:00Z'),
    })
    expect(nora!.find('time').attributes('title')).toBe(formatDateTime('2099-05-05T10:00:00Z'))
    expect(link(nora!, t(`${columns}.guardianEmail`))).toBe('mailto:gabriela@example.test')
    expect(link(nora!, t(`${columns}.guardianPhone`))).toBe('tel:600555666')
    expect(fields(hugo!)[t(`${columns}.age`)]).toBe(
      i18n.global.t('features.leaderRoster.ageYears', { age: 1 }, 1),
    )
    expect(fields(hugo!)[t(`${columns}.guardianEmail`)]).toBe('—')
    expect(hugo!.find('a').exists()).toBe(false)
  })

  it('shows a dash for missing contact data and ages', async () => {
    serveLeaderRoster([
      buildLeaderRosterActivity({
        roles: [
          {
            roleTypeId: 'role-participant',
            roleName: 'Participante',
            users: [{ firstName: 'Víctor', lastName: 'Vega', signedUpAt: '2099-05-03T10:00:00Z' }],
            dependents: [{ firstName: 'Nora', lastName: 'Gil', age: null, guardian: {} }],
          },
        ],
      }),
    ])

    const { wrapper } = await renderPanel()

    const columns = 'features.leaderRoster.columns'
    const user = fields(wrapper.find('.lr-group--users .lr-person'))
    expect(user[t(`${columns}.email`)]).toBe('—')
    expect(user[t(`${columns}.phone`)]).toBe('—')
    const dependent = fields(wrapper.find('.lr-group--dependents .lr-person'))
    expect(dependent[t(`${columns}.age`)]).toBe('—')
    expect(dependent[t(`${columns}.guardianPhone`)]).toBe('—')
    expect(wrapper.find('.lr-person a').exists()).toBe(false)
  })

  it('keeps contact links to a single address and a dialable number', async () => {
    serveLeaderRoster([
      buildLeaderRosterActivity({
        roles: [
          {
            roleTypeId: 'role-participant',
            roleName: 'Participante',
            users: [
              {
                firstName: 'Eva',
                lastName: 'Ruiz',
                email: 'eva@example.test?cc=spy@example.test&body=hola',
                phone: '+34 (600) 111-222',
                signedUpAt: '2099-05-03T10:00:00Z',
              },
            ],
            dependents: [],
          },
        ],
      }),
    ])

    const { wrapper } = await renderPanel()

    const person = wrapper.find('.lr-group--users .lr-person')
    const columns = 'features.leaderRoster.columns'
    expect(link(person, t(`${columns}.email`))).toBe(
      'mailto:eva@example.test%3Fcc%3Dspy@example.test%26body%3Dhola',
    )
    expect(link(person, t(`${columns}.phone`))).toBe('tel:+34600111222')
  })

  it('shows the empty message when the user leads no running activity', async () => {
    serveLeaderRoster([])

    const { wrapper } = await renderPanel()

    expect(wrapper.find('.data-state').text()).toBe(t('features.leaderRoster.empty'))
    expect(wrapper.find('.lr-activity').exists()).toBe(false)
  })

  it('shows the error message when the roster cannot be loaded', async () => {
    serveLeaderRoster('error')

    const { wrapper } = await renderPanel()

    await vi.waitFor(() =>
      expect(wrapper.find('.data-state--error').text()).toBe(t('features.leaderRoster.loadError')),
    )
  })

  it('never requests the roster for a guest', async () => {
    const requests = serveLeaderRoster([buildLeaderRosterActivity()])

    const { wrapper } = await renderPanel(false)

    expect(requests).toEqual([])
    expect(wrapper.find('.lr-activity').exists()).toBe(false)
  })
})
