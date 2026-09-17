import { ElButton, ElSelect } from 'element-plus'
import { describe, expect, it } from 'vitest'

import type { TimelineActivity } from '@/pages/event-detail/model/types'
import ActivityTimelineCard from '@/pages/event-detail/ui/ActivityTimelineCard.vue'
import { formatTimeRange } from '@/shared/lib'

import { ROLES } from '../../../support/fixtures/public-dashboard/signup-api'
import { renderWithProviders, t } from '../../../support/render'

function buildTimelineActivity(overrides: Partial<TimelineActivity> = {}): TimelineActivity {
  return {
    id: 'activity-1',
    title: 'Taller de robótica',
    description: 'Construye tu primer robot',
    location: 'Aula 3',
    modality: 'Presencial',
    start: new Date(2099, 5, 10, 9, 0),
    end: new Date(2099, 5, 10, 11, 0),
    highDemandRoleIds: [],
    assignment: null,
    household: [],
    ...overrides,
  }
}

interface CardProps {
  activity?: TimelineActivity
  roles?: readonly { id: string; name: string }[]
  rolesLoading?: boolean
  busy?: boolean
  authenticated?: boolean
  signupOpen?: boolean
  earlyOnly?: boolean
  hasHousehold?: boolean
  referenceDate?: Date | null
}

function renderCard(props: CardProps = {}) {
  return renderWithProviders(ActivityTimelineCard, {
    props: {
      activity: buildTimelineActivity(),
      roles: [ROLES[0]],
      rolesLoading: false,
      busy: false,
      authenticated: true,
      signupOpen: true,
      hasHousehold: false,
      ...props,
    },
  })
}

function buttonByText(wrapper: Awaited<ReturnType<typeof renderCard>>['wrapper'], text: string) {
  const button = wrapper.findAll('button').find((item) => item.text() === text)
  if (!button) throw new Error(`Missing button "${text}"`)
  return button
}

describe('ActivityTimelineCard details', () => {
  it('shows the title, schedule, modality, location and description', async () => {
    const { wrapper } = await renderCard({ referenceDate: new Date(2099, 5, 9) })
    const activity = buildTimelineActivity()

    expect(wrapper.find('.act__title').text()).toBe('Taller de robótica')
    expect(wrapper.find('.act__time').text()).toBe(
      formatTimeRange(activity.start, activity.end, new Date(2099, 5, 9)),
    )
    expect(wrapper.find('.act__meta').text()).toBe('Presencial · Aula 3')
    expect(wrapper.find('.act__desc').text()).toBe('Construye tu primer robot')
    expect(wrapper.find('.act').classes()).not.toContain('act--mine')
  })

  it('omits empty metadata and labels activities without schedule', async () => {
    const { wrapper } = await renderCard({
      activity: buildTimelineActivity({
        start: null,
        end: null,
        modality: '',
        location: '',
        description: '',
      }),
    })

    expect(wrapper.find('.act__time').text()).toBe(t('pages.eventDetail.card.noSchedule'))
    expect(wrapper.find('.act__meta').exists()).toBe(false)
    expect(wrapper.find('.act__desc').exists()).toBe(false)
  })

  it('shows only the location when there is no modality', async () => {
    const { wrapper } = await renderCard({
      activity: buildTimelineActivity({ modality: '' }),
    })

    expect(wrapper.find('.act__meta').text()).toBe('Aula 3')
  })
})

describe('ActivityTimelineCard for guests', () => {
  it('asks the visitor to sign in', async () => {
    const { wrapper } = await renderCard({ authenticated: false })

    await buttonByText(wrapper, t('pages.eventDetail.card.loginToSignup')).trigger('click')

    expect(wrapper.emitted('login')).toHaveLength(1)
    expect(wrapper.find('.el-select').exists()).toBe(false)
  })
})

describe('ActivityTimelineCard self signup', () => {
  it('preselects the only role and emits it on signup', async () => {
    const { wrapper } = await renderCard()

    expect(wrapper.find('.el-select').exists()).toBe(false)
    await buttonByText(wrapper, t('pages.eventDetail.card.enrollSelf')).trigger('click')

    expect(wrapper.emitted('signup')).toEqual([['role-participant']])
  })

  it('requires choosing a role when several are allowed and warns about high demand', async () => {
    const { wrapper } = await renderCard({
      roles: ROLES,
      activity: buildTimelineActivity({ highDemandRoleIds: ['role-volunteer'] }),
    })

    const enroll = buttonByText(wrapper, t('pages.eventDetail.card.enrollSelf'))
    expect(enroll.attributes('disabled')).toBeDefined()
    await enroll.trigger('click')
    // Even a click that bypasses the disabled button must not sign up without a role.
    wrapper.findComponent(ElButton).vm.$emit('click', new MouseEvent('click'))
    expect(wrapper.emitted('signup')).toBeUndefined()

    wrapper.findComponent(ElSelect).vm.$emit('update:modelValue', 'role-participant')
    await wrapper.vm.$nextTick()
    expect(wrapper.find('.act__demand').exists()).toBe(false)

    wrapper.findComponent(ElSelect).vm.$emit('update:modelValue', 'role-volunteer')
    await wrapper.vm.$nextTick()
    expect(wrapper.find('.act__demand').text()).toBe(t('pages.eventDetail.highDemandWarning'))

    await buttonByText(wrapper, t('pages.eventDetail.card.enrollSelf')).trigger('click')
    expect(wrapper.emitted('signup')).toEqual([['role-volunteer']])
  })

  it('selects the role once the only allowed role arrives', async () => {
    const { wrapper } = await renderCard({ roles: [] })
    expect(wrapper.find('.act__note').text()).toBe(t('pages.eventDetail.activities.rolesLoadError'))

    await wrapper.setProps({ roles: [ROLES[1]] })
    await buttonByText(wrapper, t('pages.eventDetail.card.enrollSelf')).trigger('click')

    expect(wrapper.emitted('signup')).toEqual([['role-volunteer']])
  })

  it('shows a loading note while the roles load', async () => {
    const { wrapper } = await renderCard({ rolesLoading: true })

    expect(wrapper.find('.act__note').text()).toBe(t('pages.eventDetail.card.loadingRoles'))
  })

  it('explains why signup is closed', async () => {
    const closed = await renderCard({ signupOpen: false })
    expect(closed.wrapper.find('.act__note').text()).toBe(t('pages.eventDetail.card.signupClosed'))

    const early = await renderCard({ signupOpen: false, earlyOnly: true })
    expect(early.wrapper.find('.act__note').text()).toBe(
      t('pages.eventDetail.card.earlySignupOnly'),
    )
  })
})

describe('ActivityTimelineCard with an existing enrollment', () => {
  it.each([
    ['Confirmada', 'success'],
    ['Aceptada', 'success'],
    ['Aprobada', 'success'],
    ['Rechazada', 'danger'],
    ['Denegada', 'danger'],
    ['Cancelada', 'danger'],
    ['Solicitada', 'info'],
  ])('tags the %s status as %s', async (status, severity) => {
    const { wrapper } = await renderCard({
      activity: buildTimelineActivity({ assignment: { status, roleName: 'Participante' } }),
    })

    const tag = wrapper.find('.act__head .el-tag')
    expect(tag.text()).toBe(status)
    expect(tag.classes()).toContain(`el-tag--${severity}`)
  })

  it('shows the enrolled role and lets the user withdraw while signup is open', async () => {
    const { wrapper } = await renderCard({
      activity: buildTimelineActivity({
        assignment: { status: 'Confirmada', roleName: '' },
        highDemandRoleIds: ['role-participant'],
      }),
    })

    expect(wrapper.find('.act').classes()).toContain('act--mine')
    expect(wrapper.find('.act__note').text()).toBe(
      t('pages.eventDetail.card.enrolledAs', { role: '—' }),
    )
    expect(wrapper.find('.act__demand').exists()).toBe(false)
    await buttonByText(wrapper, t('pages.eventDetail.card.unassignSelf')).trigger('click')

    expect(wrapper.emitted('unassign')).toHaveLength(1)
  })

  it('tells the user the signup period ended instead of offering withdrawal', async () => {
    const { wrapper } = await renderCard({
      signupOpen: false,
      activity: buildTimelineActivity({
        assignment: { status: 'Confirmada', roleName: 'Participante' },
      }),
    })

    const notes = wrapper.findAll('.act__note').map((note) => note.text())
    expect(notes).toEqual([
      t('pages.eventDetail.card.enrolledAs', { role: 'Participante' }),
      t('pages.eventDetail.card.signupEnded'),
    ])
  })
})

describe('ActivityTimelineCard for households', () => {
  const household = [
    { userId: 'user-1', name: 'Ada Lovelace', roleName: 'Participante', status: 'Confirmada' },
    { userId: 'child-1', name: 'Byron King', roleName: '', status: 'Pendiente' },
  ]

  it('lists enrolled members and emits the member to withdraw', async () => {
    const { wrapper } = await renderCard({
      hasHousehold: true,
      activity: buildTimelineActivity({
        household,
        assignment: { status: 'Confirmada', roleName: 'Participante' },
      }),
    })

    expect(wrapper.find('.act__head .el-tag').exists()).toBe(false)
    const members = wrapper.findAll('.act__member')
    expect(members.map((member) => member.find('b').text())).toEqual(['Ada Lovelace', 'Byron King'])
    expect(members[1]?.text()).toContain('—')
    expect(members[1]?.find('.el-tag').classes()).toContain('el-tag--info')

    await members[1]?.find('.act__member-remove').trigger('click')
    expect(wrapper.emitted('unassignMember')).toEqual([['child-1']])

    await buttonByText(wrapper, t('pages.eventDetail.card.enrollAnother')).trigger('click')
    expect(wrapper.emitted('household')).toHaveLength(1)
  })

  it('disables withdrawal while busy', async () => {
    const { wrapper } = await renderCard({
      hasHousehold: true,
      busy: true,
      activity: buildTimelineActivity({ household }),
    })

    expect(wrapper.find('.act__member-remove').attributes('disabled')).toBeDefined()
  })

  it('offers to enroll the family when nobody is enrolled yet', async () => {
    const { wrapper } = await renderCard({ hasHousehold: true, roles: ROLES })

    await buttonByText(wrapper, t('pages.eventDetail.card.enrollFamily')).trigger('click')

    expect(wrapper.emitted('household')).toHaveLength(1)
    expect(wrapper.find('.act__demand').exists()).toBe(false)
  })

  it('hides household actions once signup closes', async () => {
    const enrolled = await renderCard({
      hasHousehold: true,
      signupOpen: false,
      activity: buildTimelineActivity({ household }),
    })
    expect(enrolled.wrapper.find('.act__member-remove').exists()).toBe(false)
    expect(enrolled.wrapper.find('.act__note').exists()).toBe(false)
    expect(enrolled.wrapper.find('.el-button').exists()).toBe(false)

    const empty = await renderCard({ hasHousehold: true, signupOpen: false, earlyOnly: true })
    expect(empty.wrapper.find('.act__note').text()).toBe(
      t('pages.eventDetail.card.earlySignupOnly'),
    )
  })
})
