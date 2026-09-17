import { ElSelect } from 'element-plus'
import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import ActivityTimelineCard from '@/pages/event-detail/ui/ActivityTimelineCard.vue'
import EventActivitiesTimeline from '@/pages/event-detail/ui/EventActivitiesTimeline.vue'
import { formatDateTime, formatDateTimeRange } from '@/shared/lib'

import { buildActivityResponse, omit } from '../../../support/fixtures/public-dashboard/builders'
import {
  buttonByText,
  clickElement,
  hasButton,
  notifications,
  openDialog,
  textOf,
} from '../../../support/fixtures/public-dashboard/dom'
import { CHILD, serveSignupApi } from '../../../support/fixtures/public-dashboard/signup-api'
import { renderWithProviders, t } from '../../../support/render'
import { apiError, http, server } from '../../../support/server'

const TERMS = { id: 'terms-1', name: 'Normas del campamento', description: 'Respeta a los demás.' }

interface TimelineProps {
  signupOpen?: boolean
  earlyOnly?: boolean
  terms?: typeof TERMS | null
}

async function renderTimeline(
  props: TimelineProps = {},
  options: { guest?: boolean; realTransitions?: boolean } = {},
) {
  const rendered = await renderWithProviders(EventActivitiesTimeline, {
    props: { eventId: 'event-1', signupOpen: true, ...props },
    attach: true,
    // Dialogs only report closing through the header button once their leave transition ends.
    ...(options.realTransitions ? { stubs: { transition: false } } : {}),
    ...(options.guest ? {} : { user: { id: 'user-1', firstName: 'Ada' } }),
  })
  await vi.waitFor(() => expect(rendered.wrapper.find('.act').exists()).toBe(true))
  await flushPromises()
  return rendered
}

function waitForNotification(title: string) {
  return vi.waitFor(() => {
    const found = notifications().find((item) => item.title === title)
    expect(found).toBeDefined()
    return found
  })
}

describe('activities timeline states', () => {
  it('shows a loading message while the activities load', async () => {
    serveSignupApi()
    server.use(http.get('/api/activities', () => new Promise<never>(() => undefined)))

    const { wrapper } = await renderWithProviders(EventActivitiesTimeline, {
      props: { eventId: 'event-1', signupOpen: true },
    })

    expect(wrapper.find('.activities__state').text()).toBe(
      t('pages.eventDetail.activities.loading'),
    )
  })

  it('shows an error message when the activities cannot be loaded', async () => {
    serveSignupApi()
    server.use(http.get('/api/activities', () => apiError(500)))

    const { wrapper } = await renderWithProviders(EventActivitiesTimeline, {
      props: { eventId: 'event-1', signupOpen: true },
    })

    await vi.waitFor(() =>
      expect(wrapper.find('.activities__state').text()).toBe(
        t('pages.eventDetail.activities.loadError'),
      ),
    )
  })

  it('tells the user when the event has no activities', async () => {
    serveSignupApi({ activities: [] })

    const { wrapper } = await renderWithProviders(EventActivitiesTimeline, {
      props: { eventId: 'event-1', signupOpen: true },
    })

    await vi.waitFor(() =>
      expect(wrapper.find('.activities__state').text()).toBe(
        t('pages.eventDetail.activities.empty'),
      ),
    )
  })
})

describe('activities timeline layout', () => {
  it('groups overlapping activities and lists unscheduled ones apart', async () => {
    serveSignupApi({
      activities: [
        buildActivityResponse({
          id: 'a',
          title: 'Robótica',
          activityStartsAt: '2099-06-10T09:00:00Z',
          activityEndsAt: '2099-06-10T11:00:00Z',
        }),
        buildActivityResponse({
          id: 'b',
          title: 'Scratch',
          activityStartsAt: '2099-06-10T10:00:00Z',
          activityEndsAt: '2099-06-10T12:00:00Z',
        }),
        buildActivityResponse({
          id: 'c',
          title: 'Comida',
          activityStartsAt: '2099-06-10T13:00:00Z',
        }),
        buildActivityResponse({
          id: 'd',
          title: 'Juegos',
          activityStartsAt: '2099-06-10T14:00:00Z',
          activityEndsAt: '2099-06-10T15:00:00Z',
        }),
        omit(buildActivityResponse({ id: 'e', title: 'Por decidir' }), 'activityStartsAt'),
      ],
    })

    const { wrapper } = await renderTimeline()

    const nodes = wrapper.findAll('.tl-node')
    expect(nodes).toHaveLength(3)
    expect(nodes[0]?.find('.tl-time').text()).toContain(formatDateTime('2099-06-10T09:00:00.000Z'))
    expect(nodes[0]?.find('.tl-simul').text()).toContain('2')
    expect(nodes[0]?.findAll('.act__title').map((title) => title.text())).toEqual([
      'Robótica',
      'Scratch',
    ])
    expect(nodes[1]?.find('.tl-simul').exists()).toBe(false)
    expect(nodes[2]?.find('.act__title').text()).toBe('Juegos')

    const unscheduled = wrapper.find('.unscheduled')
    expect(unscheduled.find('h3').text()).toBe(t('pages.eventDetail.activities.noSchedule'))
    expect(unscheduled.find('.act__title').text()).toBe('Por decidir')
  })

  it('handles the actions of unscheduled activities like scheduled ones', async () => {
    const { calls } = serveSignupApi({
      children: [CHILD],
      activities: [omit(buildActivityResponse({ id: 'loose' }), 'activityStartsAt')],
    })

    const { wrapper, router } = await renderTimeline()
    await vi.waitFor(() => expect(hasButton(t('pages.eventDetail.card.enrollFamily'))).toBe(true))
    const card = wrapper.find('.unscheduled').findComponent(ActivityTimelineCard)

    card.vm.$emit('household')
    await flushPromises()
    expect(openDialog(t('pages.eventDetail.household.header'))).toBeDefined()

    card.vm.$emit('signup', 'role-participant')
    await vi.waitFor(() => expect(calls.assign).toHaveLength(1))
    expect(calls.assign[0]?.path).toBe('loose/user-1')

    card.vm.$emit('unassign')
    card.vm.$emit('unassign-member', 'child-1')
    await vi.waitFor(() => expect(calls.unassign).toHaveLength(2))
    expect(calls.unassign).toEqual(['loose/user-1', 'loose/child-1'])

    card.vm.$emit('login')
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login'))
  })

  it('explains that signup is closed, or reserved for early signup', async () => {
    serveSignupApi()
    const closed = await renderTimeline({ signupOpen: false })
    expect(closed.wrapper.find('.signup-closed').text()).toBe(
      t('pages.eventDetail.activities.signupClosed'),
    )
    closed.wrapper.unmount()

    const early = await renderTimeline({ signupOpen: false, earlyOnly: true })
    expect(early.wrapper.find('.signup-closed').text()).toBe(
      t('pages.eventDetail.activities.earlySignupOnly'),
    )
  })

  it('lets the user retry when the signup roles fail to load', async () => {
    const { state, calls } = serveSignupApi({ signupRoles: 'error' })

    const { wrapper } = await renderTimeline()
    await vi.waitFor(() => expect(wrapper.find('.signup-closed').exists()).toBe(true))
    expect(wrapper.find('.signup-closed').text()).toContain(
      t('pages.eventDetail.activities.rolesLoadError'),
    )

    state.signupRoles = [{ userId: 'user-1', roles: [{ id: 'role-participant', name: 'P' }] }]
    await clickElement(buttonByText(t('common.retry')))

    await vi.waitFor(() => expect(wrapper.find('.signup-closed').exists()).toBe(false))
    expect(calls.counts.signupRoles).toBe(2)
    expect(hasButton(t('pages.eventDetail.card.enrollSelf'))).toBe(true)
  })

  it('sends guests to the login page and back to the event afterwards', async () => {
    serveSignupApi()

    const { router } = await renderTimeline({}, { guest: true })
    await clickElement(buttonByText(t('pages.eventDetail.card.loginToSignup')))

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login'))
    expect(router.currentRoute.value.query).toEqual({ redirect: '/events/event-1' })
  })

  it('ignores withdrawal requests from guests', async () => {
    const { calls } = serveSignupApi()

    const { wrapper } = await renderTimeline({}, { guest: true })
    wrapper.findComponent(ActivityTimelineCard).vm.$emit('unassign')
    await flushPromises()

    expect(calls.unassign).toEqual([])
  })
})

describe('activities timeline self signup', () => {
  it('checks overlaps, signs the user up and confirms it', async () => {
    const { calls } = serveSignupApi()

    await renderTimeline()
    await clickElement(buttonByText(t('pages.eventDetail.card.enrollSelf')))

    await vi.waitFor(() => expect(calls.assign).toHaveLength(1))
    expect(calls.overlapChecks).toEqual(['activity-1/user-1'])
    expect(calls.assign[0]).toMatchObject({
      path: 'activity-1/user-1',
      body: { activityRoleTypeId: 'role-participant', acceptTerms: false },
    })
    const toast = await waitForNotification(t('pages.eventDetail.toast.signupSent'))
    expect(toast?.message).toBe(t('pages.eventDetail.toast.signupSuccess'))
  })

  it('asks for confirmation when the activity overlaps another signup', async () => {
    const { calls } = serveSignupApi({
      overlap: {
        hasOverlaps: true,
        overlaps: [
          {
            activityId: 'other',
            title: 'Taller de Scratch',
            startsAt: '2099-06-10T09:30:00Z',
            endsAt: '2099-06-10T10:30:00Z',
          },
        ],
      },
    })

    await renderTimeline()
    await clickElement(buttonByText(t('pages.eventDetail.card.enrollSelf')))

    const dialog = await vi.waitFor(() => {
      const found = openDialog(t('pages.eventDetail.overlap.header'))
      expect(found).toBeDefined()
      return found as HTMLElement
    })
    expect(textOf(dialog.querySelector('.overlap__list strong'))).toBe('Taller de Scratch')
    expect(textOf(dialog.querySelector('.overlap__when'))).toBe(
      formatDateTimeRange('2099-06-10T09:30:00Z', '2099-06-10T10:30:00Z'),
    )
    expect(calls.assign).toEqual([])

    await clickElement(buttonByText(t('pages.eventDetail.overlap.enrollAnyway'), dialog))

    await vi.waitFor(() => expect(calls.assign).toHaveLength(1))
    expect(openDialog(t('pages.eventDetail.overlap.header'))).toBeUndefined()
  })

  it('does not sign up when the overlap warning is cancelled', async () => {
    const { calls } = serveSignupApi({
      overlap: { hasOverlaps: true, overlaps: null },
    })

    await renderTimeline({}, { realTransitions: true })
    await clickElement(buttonByText(t('pages.eventDetail.card.enrollSelf')))
    const dialog = await vi.waitFor(() => {
      const found = openDialog(t('pages.eventDetail.overlap.header'))
      expect(found).toBeDefined()
      return found as HTMLElement
    })
    expect(dialog.querySelectorAll('.overlap__list li')).toHaveLength(0)

    await clickElement(dialog.querySelector<HTMLElement>('.el-dialog__headerbtn') as HTMLElement)
    await vi.waitFor(() =>
      expect(openDialog(t('pages.eventDetail.overlap.header'))).toBeUndefined(),
    )

    await clickElement(buttonByText(t('pages.eventDetail.card.enrollSelf')))
    await vi.waitFor(() => expect(openDialog(t('pages.eventDetail.overlap.header'))).toBeDefined())
    await clickElement(buttonByText(t('common.cancel'), dialog))

    await vi.waitFor(() =>
      expect(openDialog(t('pages.eventDetail.overlap.header'))).toBeUndefined(),
    )
    expect(calls.overlapChecks).toHaveLength(2)
    expect(calls.assign).toEqual([])
  })

  it('reports a failed overlap check', async () => {
    const { calls } = serveSignupApi({ overlap: 'error' })

    await renderTimeline()
    await clickElement(buttonByText(t('pages.eventDetail.card.enrollSelf')))

    await waitForNotification(t('common.error'))
    expect(calls.assign).toEqual([])
  })

  it('ignores signups while signup is closed', async () => {
    const { calls } = serveSignupApi()

    const { wrapper } = await renderTimeline({ signupOpen: false })
    wrapper.findComponent(ActivityTimelineCard).vm.$emit('signup', 'role-participant')
    wrapper.findComponent(ActivityTimelineCard).vm.$emit('unassign-member', 'child-1')
    await flushPromises()

    expect(calls.overlapChecks).toEqual([])
    expect(calls.unassign).toEqual([])
  })

  it('asks to accept pending terms before signing up', async () => {
    const { calls } = serveSignupApi({ termsAccepted: false })

    await renderTimeline({ terms: TERMS })
    await vi.waitFor(() => expect(calls.counts.terms).toBe(1))
    await clickElement(buttonByText(t('pages.eventDetail.card.enrollSelf')))

    const dialog = await vi.waitFor(() => {
      const found = openDialog(t('pages.eventDetail.terms.header'))
      expect(found).toBeDefined()
      return found as HTMLElement
    })
    expect(textOf(dialog.querySelector('.terms__lead b'))).toBe(TERMS.name)
    expect(textOf(dialog.querySelector('.terms__content'))).toBe(TERMS.description)
    expect(calls.assign).toEqual([])

    await clickElement(buttonByText(t('pages.eventDetail.terms.accept'), dialog))

    await vi.waitFor(() => expect(calls.assign).toHaveLength(1))
    expect(calls.assign[0]?.body).toEqual({
      activityRoleTypeId: 'role-participant',
      acceptTerms: true,
    })
  })

  it('does not sign up when the terms are declined', async () => {
    const { calls } = serveSignupApi({ termsAccepted: false })

    await renderTimeline({ terms: TERMS }, { realTransitions: true })
    await vi.waitFor(() => expect(calls.counts.terms).toBe(1))
    await clickElement(buttonByText(t('pages.eventDetail.card.enrollSelf')))
    const dialog = await vi.waitFor(() => {
      const found = openDialog(t('pages.eventDetail.terms.header'))
      expect(found).toBeDefined()
      return found as HTMLElement
    })

    await clickElement(dialog.querySelector<HTMLElement>('.el-dialog__headerbtn') as HTMLElement)
    await vi.waitFor(() => expect(openDialog(t('pages.eventDetail.terms.header'))).toBeUndefined())

    await clickElement(buttonByText(t('pages.eventDetail.card.enrollSelf')))
    await vi.waitFor(() => expect(openDialog(t('pages.eventDetail.terms.header'))).toBeDefined())
    await clickElement(buttonByText(t('common.cancel'), dialog))

    await vi.waitFor(() => expect(openDialog(t('pages.eventDetail.terms.header'))).toBeUndefined())
    expect(calls.assign).toEqual([])
  })

  it('opens the terms when the API requires accepting them and retries with acceptance', async () => {
    const { calls, state } = serveSignupApi({
      assignError: () => apiError(400, 'EventTermsAcceptanceRequired'),
    })

    await renderTimeline({ terms: TERMS })
    await clickElement(buttonByText(t('pages.eventDetail.card.enrollSelf')))

    const dialog = await vi.waitFor(() => {
      const found = openDialog(t('pages.eventDetail.terms.header'))
      expect(found).toBeDefined()
      return found as HTMLElement
    })
    expect(notifications()).toEqual([])

    state.assignError = null
    await clickElement(buttonByText(t('pages.eventDetail.terms.accept'), dialog))

    await vi.waitFor(() => expect(calls.assign).toHaveLength(2))
    expect(calls.assign.map((call) => call.body)).toEqual([
      { activityRoleTypeId: 'role-participant', acceptTerms: false },
      { activityRoleTypeId: 'role-participant', acceptTerms: true },
    ])
  })

  it('reports a terms error as a failed signup when the event has no terms loaded', async () => {
    serveSignupApi({ assignError: () => apiError(400, 'EventTermsAcceptanceRequired') })

    await renderTimeline()
    await clickElement(buttonByText(t('pages.eventDetail.card.enrollSelf')))

    await waitForNotification(t('pages.eventDetail.toast.signupFailed'))
    expect(openDialog(t('pages.eventDetail.terms.header'))).toBeUndefined()
  })

  it('reports other signup failures', async () => {
    serveSignupApi({ assignError: () => apiError(500) })

    await renderTimeline()
    await clickElement(buttonByText(t('pages.eventDetail.card.enrollSelf')))

    const toast = await waitForNotification(t('pages.eventDetail.toast.signupFailed'))
    expect(toast?.message).toContain('trace-123')
  })
})

describe('activities timeline withdrawal', () => {
  const assigned = [
    {
      activityId: 'activity-1',
      status: { name: 'Confirmada' },
      roleType: { name: 'Participante' },
    },
    { status: { name: 'Huérfana' } },
  ]

  it('withdraws the user from an activity', async () => {
    const { calls } = serveSignupApi({ assigned })

    const { wrapper } = await renderTimeline()
    await vi.waitFor(() => expect(hasButton(t('pages.eventDetail.card.unassignSelf'))).toBe(true))
    expect(wrapper.find('.act__head .el-tag').text()).toBe('Confirmada')

    await clickElement(buttonByText(t('pages.eventDetail.card.unassignSelf')))

    await vi.waitFor(() => expect(calls.unassign).toEqual(['activity-1/user-1']))
    const toast = await waitForNotification(t('pages.eventDetail.toast.unassignSummary'))
    expect(toast?.message).toBe(t('pages.eventDetail.toast.unassignSuccess'))
  })

  it('reports a failed withdrawal', async () => {
    serveSignupApi({ assigned, unassignError: () => apiError(500) })

    await renderTimeline()
    await vi.waitFor(() => expect(hasButton(t('pages.eventDetail.card.unassignSelf'))).toBe(true))
    await clickElement(buttonByText(t('pages.eventDetail.card.unassignSelf')))

    await waitForNotification(t('common.error'))
  })
})

describe('activities timeline household signup', () => {
  async function openHousehold(
    props: TimelineProps = {},
    options: { realTransitions?: boolean } = {},
  ) {
    const rendered = await renderTimeline(props, options)
    await vi.waitFor(() => expect(hasButton(t('pages.eventDetail.card.enrollFamily'))).toBe(true))
    await clickElement(buttonByText(t('pages.eventDetail.card.enrollFamily')))
    const dialog = await vi.waitFor(() => {
      const found = openDialog(t('pages.eventDetail.household.header'))
      expect(found).toBeDefined()
      return found as HTMLElement
    })
    return { ...rendered, dialog }
  }

  it('lists the user and their minors and requires a role for each included member', async () => {
    const { calls } = serveSignupApi({ children: [CHILD] })

    const { wrapper, dialog } = await openHousehold()

    expect(textOf(dialog.querySelector('.household__lead b'))).toBe('Taller de robótica')
    expect([...dialog.querySelectorAll('.household__name')].map(textOf)).toEqual([
      'Ada',
      'Byron King',
    ])
    const selects = wrapper.findAllComponents(ElSelect)
    expect(selects.map((select) => select.props('modelValue'))).toEqual(['role-participant', ''])

    await clickElement(buttonByText(t('pages.eventDetail.household.enroll'), dialog))
    const warning = await waitForNotification(t('pages.eventDetail.toast.missingRole'))
    expect(warning?.message).toBe(t('pages.eventDetail.toast.missingRoleDetail'))
    expect(calls.household).toEqual([])

    selects[1]?.vm.$emit('update:modelValue', 'role-volunteer')
    await flushPromises()
    await clickElement(buttonByText(t('pages.eventDetail.household.enroll'), dialog))

    await vi.waitFor(() => expect(calls.household).toHaveLength(1))
    expect(calls.household[0]).toEqual({
      path: 'activity-1',
      body: {
        assignments: [
          { userId: 'user-1', activityRoleTypeId: 'role-participant' },
          { userId: 'child-1', activityRoleTypeId: 'role-volunteer' },
        ],
        acceptTerms: false,
      },
    })
    const toast = await waitForNotification(t('pages.eventDetail.toast.signupSent'))
    expect(toast?.message).toBe(t('pages.eventDetail.toast.householdSuccess'))
    expect(openDialog(t('pages.eventDetail.household.header'))).toBeUndefined()
  })

  it('warns when an included member picks a high-demand role', async () => {
    serveSignupApi({
      children: [CHILD],
      activities: [
        buildActivityResponse({
          roleCapacities: [
            { activityRoleTypeId: 'role-participant', isHighDemand: true },
            { activityRoleTypeId: 'role-volunteer', isHighDemand: false },
          ],
        }),
      ],
    })

    const { dialog } = await openHousehold()

    expect(textOf(dialog.querySelector('.household__demand'))).toBe(
      t('pages.eventDetail.highDemandWarning'),
    )

    const selfCheckbox = dialog.querySelector<HTMLInputElement>('#hh-user-1 input, input#hh-user-1')
    await clickElement(
      selfCheckbox ?? (dialog.querySelector('input[type="checkbox"]') as HTMLElement),
    )

    expect(dialog.querySelector('.household__demand')).toBeNull()
  })

  it('closes without a request when nobody is included', async () => {
    const { calls } = serveSignupApi({ children: [CHILD] })

    const { dialog } = await openHousehold()
    for (const checkbox of dialog.querySelectorAll<HTMLInputElement>('input[type="checkbox"]')) {
      await clickElement(checkbox)
    }
    await clickElement(buttonByText(t('pages.eventDetail.household.enroll'), dialog))

    expect(openDialog(t('pages.eventDetail.household.header'))).toBeUndefined()
    expect(calls.household).toEqual([])
    expect(notifications()).toEqual([])
  })

  it('closes when cancelled or dismissed', async () => {
    const { calls } = serveSignupApi({ children: [CHILD] })

    const { dialog } = await openHousehold({}, { realTransitions: true })
    await clickElement(buttonByText(t('common.cancel'), dialog))
    await vi.waitFor(() =>
      expect(openDialog(t('pages.eventDetail.household.header'))).toBeUndefined(),
    )

    await clickElement(buttonByText(t('pages.eventDetail.card.enrollFamily')))
    await vi.waitFor(() =>
      expect(openDialog(t('pages.eventDetail.household.header'))).toBeDefined(),
    )
    await clickElement(dialog.querySelector<HTMLElement>('.el-dialog__headerbtn') as HTMLElement)

    await vi.waitFor(() =>
      expect(openDialog(t('pages.eventDetail.household.header'))).toBeUndefined(),
    )
    expect(calls.household).toEqual([])
  })

  it('marks members already enrolled and disables enrolling when everyone is in', async () => {
    serveSignupApi({
      children: [CHILD],
      household: [
        {
          activityId: 'activity-1',
          userId: 'user-1',
          firstName: 'Ada',
          lastName: 'Lovelace',
          roleName: 'Participante',
          statusName: 'Confirmada',
        },
        {
          activityId: 'activity-1',
          userId: 'child-1',
          firstName: 'Byron',
          lastName: 'King',
          statusName: 'Pendiente',
        },
        { userId: 'ghost', firstName: 'Sin actividad' },
      ],
    })

    await renderTimeline()
    await vi.waitFor(() => expect(hasButton(t('pages.eventDetail.card.enrollAnother'))).toBe(true))
    await clickElement(buttonByText(t('pages.eventDetail.card.enrollAnother')))
    const dialog = await vi.waitFor(() => {
      const found = openDialog(t('pages.eventDetail.household.header'))
      expect(found).toBeDefined()
      return found as HTMLElement
    })

    expect([...dialog.querySelectorAll('.household__already')].map(textOf)).toEqual([
      t('pages.eventDetail.household.alreadyAs', { role: 'Participante' }),
      t('pages.eventDetail.household.alreadyAs', { role: '—' }),
    ])
    expect(dialog.querySelectorAll('input[type="checkbox"]')).toHaveLength(0)
    expect(textOf(dialog.querySelector('.household__note'))).toBe(
      t('pages.eventDetail.household.allInscribed'),
    )
    expect(buttonByText(t('pages.eventDetail.household.enroll'), dialog).disabled).toBe(true)
  })

  it('withdraws a single household member', async () => {
    const { calls } = serveSignupApi({
      children: [CHILD],
      household: [
        {
          activityId: 'activity-1',
          userId: 'child-1',
          firstName: 'Byron',
          lastName: 'King',
          roleName: 'Participante',
          statusName: 'Confirmada',
        },
      ],
    })

    const { wrapper } = await renderTimeline()
    await vi.waitFor(() => expect(wrapper.find('.act__member-remove').exists()).toBe(true))
    await wrapper.find('.act__member-remove').trigger('click')

    await vi.waitFor(() => expect(calls.unassign).toEqual(['activity-1/child-1']))
  })

  it('asks for pending terms before enrolling the household', async () => {
    const { calls } = serveSignupApi({ children: [CHILD], termsAccepted: false })

    const { wrapper, dialog } = await openHousehold({ terms: TERMS })
    await vi.waitFor(() => expect(calls.counts.terms).toBe(1))
    for (const checkbox of dialog.querySelectorAll<HTMLInputElement>('input[type="checkbox"]')) {
      if (checkbox.closest('.household__row')?.textContent?.includes('Byron')) {
        await clickElement(checkbox)
      }
    }
    expect(wrapper.findAllComponents(ElSelect)[1]?.props('disabled')).toBe(true)
    await clickElement(buttonByText(t('pages.eventDetail.household.enroll'), dialog))

    const terms = await vi.waitFor(() => {
      const found = openDialog(t('pages.eventDetail.terms.header'))
      expect(found).toBeDefined()
      return found as HTMLElement
    })
    expect(calls.household).toEqual([])
    await clickElement(buttonByText(t('pages.eventDetail.terms.accept'), terms))

    await vi.waitFor(() => expect(calls.household).toHaveLength(1))
    expect(calls.household[0]?.body).toEqual({
      assignments: [{ userId: 'user-1', activityRoleTypeId: 'role-participant' }],
      acceptTerms: true,
    })
  })

  it('retries the household signup with acceptance when the API requires the terms', async () => {
    const { calls, state } = serveSignupApi({
      children: [CHILD],
      householdError: () => apiError(400, 'EventTermsAcceptanceRequired'),
    })

    const { dialog } = await openHousehold({ terms: TERMS })
    for (const checkbox of dialog.querySelectorAll<HTMLInputElement>('input[type="checkbox"]')) {
      if (checkbox.closest('.household__row')?.textContent?.includes('Byron')) {
        await clickElement(checkbox)
      }
    }
    await clickElement(buttonByText(t('pages.eventDetail.household.enroll'), dialog))

    const terms = await vi.waitFor(() => {
      const found = openDialog(t('pages.eventDetail.terms.header'))
      expect(found).toBeDefined()
      return found as HTMLElement
    })
    state.householdError = null
    await clickElement(buttonByText(t('pages.eventDetail.terms.accept'), terms))

    await vi.waitFor(() => expect(calls.household).toHaveLength(2))
    expect(calls.household.map((call) => call.body)).toEqual([
      {
        assignments: [{ userId: 'user-1', activityRoleTypeId: 'role-participant' }],
        acceptTerms: false,
      },
      {
        assignments: [{ userId: 'user-1', activityRoleTypeId: 'role-participant' }],
        acceptTerms: true,
      },
    ])
  })

  it('reports a failed household signup', async () => {
    serveSignupApi({ children: [CHILD], householdError: () => apiError(500) })

    const { wrapper, dialog } = await openHousehold()
    wrapper.findAllComponents(ElSelect)[1]?.vm.$emit('update:modelValue', 'role-volunteer')
    await flushPromises()
    await clickElement(buttonByText(t('pages.eventDetail.household.enroll'), dialog))

    await waitForNotification(t('pages.eventDetail.toast.signupFailed'))
    expect(openDialog(t('pages.eventDetail.household.header'))).toBeDefined()
  })
})
