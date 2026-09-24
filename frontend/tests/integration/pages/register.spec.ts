import type { VueWrapper } from '@vue/test-utils'
import { ElSelect } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import type { Gender, RegisterResponse } from '@/shared/api/generated/models'
import { i18n } from '@/shared/i18n'

import { useHomeApi } from '../../support/fixtures/auth-register/home'
import { buildUserResponse } from '../../support/fixtures/user'
import { renderApp, t } from '../../support/render'
import { apiError, http, HttpResponse, server, TEST_CSRF_TOKEN } from '../../support/server'

function page(wrapper: VueWrapper) {
  return wrapper.get('main')
}

async function clickButton(wrapper: VueWrapper, text: string): Promise<void> {
  const button = page(wrapper)
    .findAll('button')
    .find((candidate) => candidate.text() === text)
  if (!button) throw new Error(`button "${text}" not found`)
  await button.trigger('click')
}

async function selectGender(wrapper: VueWrapper, index: number, gender: Gender): Promise<void> {
  const select = wrapper.findAllComponents(ElSelect)[index]
  if (!select) throw new Error(`select ${index} not found`)
  select.vm.$emit('update:modelValue', gender)
  await wrapper.vm.$nextTick()
}

async function fillAdult(wrapper: VueWrapper): Promise<void> {
  await wrapper.find('#reg-firstname').setValue('Ada')
  await wrapper.find('#reg-lastname').setValue('Lovelace')
  await wrapper.find('#reg-email').setValue(' ada@example.test ')
  await wrapper.find('#reg-phone').setValue('600000000')
  await wrapper.find('#reg-password').setValue('correct-horse-battery')
  await wrapper.find('#reg-password-confirm').setValue('correct-horse-battery')
  await wrapper.find('#reg-national-id').setValue('x1234567l')
  await wrapper.find('#reg-national-id-confirm').setValue('X-1234567-L')
  await wrapper.find('#reg-promotional-consent').setValue(true)
  await selectGender(wrapper, 0, 'Female')
}

function serveRegister(response: () => Response) {
  const received: { body: unknown; csrf: string | null }[] = []
  server.use(
    http.post('/api/auth/register', async ({ request }) => {
      received.push({ body: await request.json(), csrf: request.headers.get('X-CSRF-TOKEN') })
      return response()
    }),
  )
  return received
}

describe('register page', () => {
  it('stops minors at the age gate with guardian instructions', async () => {
    const { wrapper } = await renderApp('/register')
    const heading = page(wrapper).get('.register-head')
    expect(heading.get('h1').text()).toBe(t('pages.register.title'))
    expect(heading.get('.page-heading__comment').text()).toBe(`//${t('pages.register.intro')}`)
    expect(heading.find('.eyebrow').exists()).toBe(false)

    await clickButton(wrapper, t('features.register.ageGate.decline'))

    expect(page(wrapper).text()).toContain(t('features.register.ageGate.blockedHeading'))
    expect(wrapper.find('form').exists()).toBe(false)
  })

  it('registers an adult with a minor and asks to verify the email', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] })
    const response: RegisterResponse = {
      adult: buildUserResponse({ id: 'adult-1' }),
      minors: [buildUserResponse({ id: 'minor-1' })],
    }
    const received = serveRegister(() => HttpResponse.json(response, { status: 201 }))
    const resent: unknown[] = []
    server.use(
      http.post('/api/auth/:userId/resend-verification', ({ params }) => {
        resent.push(params.userId)
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { wrapper } = await renderApp('/register')
    await clickButton(wrapper, t('features.register.ageGate.confirm'))
    await fillAdult(wrapper)
    await clickButton(wrapper, t('features.register.form.addMinor'))
    await wrapper.find('#minor-firstname-0').setValue(' Byron ')
    await wrapper.find('#minor-lastname-0').setValue('King')
    await wrapper.find('#minor-dob-0').setValue('2016-02-03')
    await selectGender(wrapper, 1, 'Male')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() =>
      expect(page(wrapper).text()).toContain(t('features.register.success.title')),
    )
    expect(received).toEqual([
      {
        csrf: TEST_CSRF_TOKEN,
        body: {
          firstName: 'Ada',
          lastName: 'Lovelace',
          email: 'ada@example.test',
          phone: '600000000',
          password: 'correct-horse-battery',
          nationalId: 'X1234567L',
          gender: 'Female',
          promotionalConsent: true,
          minors: [
            { firstName: 'Byron', lastName: 'King', birthDate: '2016-02-03', gender: 'Male' },
          ],
        },
      },
    ])
    expect(page(wrapper).text()).toContain(
      i18n.global.t('features.register.success.minorsEnrolled', { n: 1 }, 1),
    )
    expect(wrapper.get('.reg-success__verify-intro b').text()).toBe('ada@example.test')

    const resend = () => wrapper.get('.reg-success__resend-button')
    expect(resend().text()).toBe(t('features.register.success.resendCountdown', { s: 60 }))
    expect(resend().attributes('disabled')).toBeDefined()

    vi.advanceTimersByTime(60_000)
    await wrapper.vm.$nextTick()
    expect(resend().text()).toBe(t('features.register.success.resend'))

    await resend().trigger('click')
    await vi.waitFor(() => expect(resent).toEqual(['adult-1']))
    await vi.waitFor(() =>
      expect(document.body.querySelector('.el-notification')?.textContent).toContain(
        t('features.register.toast.linkResentDetail'),
      ),
    )
    await vi.waitFor(() =>
      expect(resend().text()).toBe(t('features.register.success.resendCountdown', { s: 60 })),
    )
  })

  it('always asks to verify the email and restarts the flow on demand', async () => {
    serveRegister(() =>
      HttpResponse.json({ adult: buildUserResponse(), minors: [] }, { status: 201 }),
    )
    const { wrapper } = await renderApp('/register')
    await clickButton(wrapper, t('features.register.ageGate.confirm'))
    await fillAdult(wrapper)
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() =>
      expect(page(wrapper).text()).toContain(t('features.register.success.verifyIntroBefore')),
    )
    expect(wrapper.find('.reg-success__role').exists()).toBe(false)

    await clickButton(wrapper, t('features.register.success.registerAnother'))

    expect(page(wrapper).text()).toContain(t('features.register.ageGate.question'))
    await clickButton(wrapper, t('features.register.ageGate.confirm'))
    expect((wrapper.get('#reg-firstname').element as HTMLInputElement).value).toBe('')
  })

  it('keeps the form and shows the API error when the email is already in use', async () => {
    serveRegister(() => apiError(409, 'RegisterEmailAlreadyInUse'))
    const { wrapper } = await renderApp('/register')
    await clickButton(wrapper, t('features.register.ageGate.confirm'))
    await fillAdult(wrapper)

    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() =>
      expect(document.body.querySelector('.el-notification')?.textContent).toContain(
        t('errors.RegisterEmailAlreadyInUse'),
      ),
    )
    expect(wrapper.find('form').exists()).toBe(true)
    expect((wrapper.get('#reg-firstname').element as HTMLInputElement).value).toBe('Ada')
  })

  it('does not call the API when the form is invalid', async () => {
    const received = serveRegister(() => HttpResponse.json({}, { status: 201 }))
    const { wrapper } = await renderApp('/register')
    await clickButton(wrapper, t('features.register.ageGate.confirm'))

    await wrapper.find('form').trigger('submit')
    await vi.waitFor(() => expect(wrapper.findAll('.reg__error').length).toBeGreaterThan(0))

    expect(received).toHaveLength(0)
  })

  it('returns from the form to the age gate', async () => {
    const { wrapper } = await renderApp('/register')
    await clickButton(wrapper, t('features.register.ageGate.confirm'))

    await clickButton(wrapper, t('features.register.back'))

    expect(page(wrapper).text()).toContain(t('features.register.ageGate.question'))
  })

  it('sends signed-in users home', async () => {
    useHomeApi()

    const { router } = await renderApp('/register', { user: {} })

    expect(router.currentRoute.value.name).toBe('home')
  })
})
