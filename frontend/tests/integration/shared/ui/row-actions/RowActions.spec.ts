import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { RowActions, type RowAction } from '@/shared/ui/row-actions'

import {
  click,
  findButton,
  findButtons,
  openRowMenu,
  rowMenuItem,
  rowMenuLabels,
  textOf,
} from '../../../../support/dom'
import { renderWithProviders, t } from '../../../../support/render'

function action(key: string, overrides: Partial<RowAction> = {}): RowAction {
  return { key, label: key, icon: 'pencil', run: vi.fn(), ...overrides }
}

function renderActions(actions: readonly RowAction[], menuLabel?: string) {
  return renderWithProviders(RowActions, {
    props: { actions, ...(menuLabel ? { menuLabel } : {}) },
    attach: true,
  })
}

describe('RowActions', () => {
  it('renders inline actions as labelled icon buttons that run when clicked', async () => {
    const edit = action('Edit', { inline: true })
    const remove = action('Delete', { inline: true, icon: 'trash', danger: true })
    const { wrapper } = await renderActions([edit, remove])

    const editButton = findButton('Edit', wrapper.element)
    const deleteButton = findButton('Delete', wrapper.element)
    expect(editButton.classList).toContain('row-actions__button')
    expect(editButton.classList).not.toContain('row-actions__button--danger')
    expect(deleteButton.classList).toContain('row-actions__button--danger')
    expect(editButton.querySelector('.app-icon')).not.toBeNull()

    await click(editButton)
    expect(edit.run).toHaveBeenCalledTimes(1)
    expect(remove.run).not.toHaveBeenCalled()
  })

  it('disables an inline action without running it', async () => {
    const edit = action('Edit', { inline: true, disabled: true })
    const { wrapper } = await renderActions([edit])

    const button = findButton('Edit', wrapper.element)
    expect(button.disabled).toBe(true)

    await click(button)
    expect(edit.run).not.toHaveBeenCalled()
  })

  it('renders no menu trigger without menu actions', async () => {
    const { wrapper } = await renderActions([action('Edit', { inline: true })])

    expect(wrapper.find('.row-actions__more').exists()).toBe(false)
    expect(wrapper.find('.el-dropdown').exists()).toBe(false)
  })

  it('labels the menu trigger with the default or a custom name', async () => {
    const generic = await renderActions([action('Feature')])
    const named = await renderActions([action('Feature')], 'More actions for Ada')

    const genericTrigger = generic.wrapper.find('.row-actions__more')
    expect(genericTrigger.element.tagName).toBe('BUTTON')
    expect(genericTrigger.attributes('aria-label')).toBe(t('table.moreActions'))
    expect(genericTrigger.attributes('id')).toBeTruthy()
    expect(named.wrapper.find('.row-actions__more').attributes('aria-label')).toBe(
      'More actions for Ada',
    )
    expect(findButtons(t('table.moreActions'), generic.wrapper.element)).toHaveLength(1)
  })

  it('opens a menu labelled by its trigger with the items in order', async () => {
    const { wrapper } = await renderActions([
      action('Edit', { inline: true }),
      action('Change type', { icon: 'tag' }),
      action('Grant admin', { icon: 'key', disabled: true, reason: 'Initial admin' }),
      action('Delete', { icon: 'trash', danger: true, divided: true }),
    ])

    const menu = await openRowMenu(wrapper.element)

    const trigger = wrapper.find('.row-actions__more')
    expect(menu.getAttribute('aria-labelledby')).toBe(trigger.attributes('id'))
    expect(trigger.attributes('aria-haspopup')).toBe('menu')
    expect(trigger.attributes('aria-expanded')).toBe('true')
    expect(rowMenuLabels(menu)).toEqual(['Change type', 'Grant admin', 'Delete'])

    const items = [...menu.children]
    expect(items.map((item) => item.getAttribute('role'))).toEqual([
      'menuitem',
      'menuitem',
      'separator',
      'menuitem',
    ])

    const changeType = rowMenuItem(menu, 'Change type')
    expect(changeType.getAttribute('aria-disabled')).toBeNull()
    expect(changeType.classList).not.toContain('row-actions__item--danger')
    expect(changeType.querySelector('.row-actions__item-icon')).not.toBeNull()
    expect(changeType.querySelector('.row-actions__item-reason')).toBeNull()

    const grant = rowMenuItem(menu, 'Grant admin')
    expect(grant.getAttribute('aria-disabled')).toBe('true')
    expect(textOf(grant.querySelector('.row-actions__item-reason'))).toBe('Initial admin')

    expect(rowMenuItem(menu, 'Delete').classList).toContain('row-actions__item--danger')
    expect(() => rowMenuItem(menu, 'Missing')).toThrow('Row menu item "Missing" not found')
  })

  it('hides the reason of an enabled menu item', async () => {
    const { wrapper } = await renderActions([action('Feature', { reason: 'Already featured' })])

    const menu = await openRowMenu(wrapper.element)

    expect(rowMenuItem(menu, 'Feature').querySelector('.row-actions__item-reason')).toBeNull()
  })

  it('runs an enabled menu item and closes the menu', async () => {
    const feature = action('Feature', { icon: 'star' })
    const { wrapper } = await renderActions([feature])

    const menu = await openRowMenu(wrapper.element)
    const popper = menu.closest<HTMLElement>('.el-popper')
    expect(popper?.classList).toContain('row-actions__menu')
    await click(rowMenuItem(menu, 'Feature'))

    expect(feature.run).toHaveBeenCalledTimes(1)
    await vi.waitFor(() => expect(popper?.style.display).toBe('none'))
  })

  it('does not run a disabled menu item', async () => {
    const remove = action('Delete', { disabled: true, reason: 'Your own account' })
    const { wrapper } = await renderActions([remove])

    const menu = await openRowMenu(wrapper.element)
    await click(rowMenuItem(menu, 'Delete'))
    await flushPromises()

    expect(remove.run).not.toHaveBeenCalled()
  })

  it('fails clearly when a row has no menu', async () => {
    const { wrapper } = await renderActions([action('Edit', { inline: true })])

    await expect(openRowMenu(wrapper.element)).rejects.toThrow('Row has no "more actions" menu')
  })
})
