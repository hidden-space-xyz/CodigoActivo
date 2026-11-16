import { flushPromises } from '@vue/test-utils'
import { ElColorPicker, ElOption, ElSelect } from 'element-plus'
import { describe, expect, it } from 'vitest'

import { columnTypeLabelKey } from '@/pages/admin/inventory/model/category-form'
import InventoryCategoryDialog from '@/pages/admin/inventory/ui/InventoryCategoryDialog.vue'

import { renderWithProviders, t } from '../../../../../support/render'
import { click, findButton, openDialog, typeInto } from '../../../../../support/dom'

async function renderDialog() {
  const rendered = await renderWithProviders(InventoryCategoryDialog, {
    props: { visible: false, category: null, saving: false },
    attach: true,
  })
  await rendered.wrapper.setProps({ visible: true })
  await flushPromises()
  return { ...rendered, dialog: openDialog(t('pages.admin.inventory.form.newHeader')) }
}

function items(dialog: HTMLElement, testid: string): HTMLElement[] {
  return [...dialog.querySelectorAll<HTMLElement>(`li[data-testid="${testid}"]`)]
}

async function typeIn(item: HTMLElement | undefined, testid: string, value: string) {
  const input = item?.querySelector<HTMLInputElement>(`input[data-testid="${testid}"]`)
  if (!input) throw new Error(`Input "${testid}" not rendered`)
  await typeInto(`#${input.id}`, value)
}

async function press(item: HTMLElement | undefined, labelKey: string) {
  if (!item) throw new Error('Item not rendered')
  await click(findButton(t(labelKey), item))
}

describe('InventoryCategoryDialog', () => {
  it('reorders and removes new columns and options and saves them as arranged', async () => {
    const { wrapper, dialog } = await renderDialog()
    const selects = () => wrapper.findAllComponents(ElSelect)
    await typeInto('#inventory-category-name', 'Camisetas')
    await typeIn(items(dialog, 'inventory-column')[0], 'inventory-column-name', 'Talla')
    selects()[0]?.vm.$emit('update:modelValue', 'List')
    await flushPromises()
    await typeIn(items(dialog, 'inventory-option')[0], 'inventory-option-name', 'S')
    await click(findButton(t('pages.admin.inventory.form.addOption'), dialog))
    await typeIn(items(dialog, 'inventory-option')[1], 'inventory-option-name', 'M')
    await click(findButton(t('pages.admin.inventory.form.addOption'), dialog))
    await press(items(dialog, 'inventory-option')[2], 'pages.admin.inventory.form.removeOption')
    await press(items(dialog, 'inventory-option')[1], 'pages.admin.inventory.form.moveUp')
    await press(items(dialog, 'inventory-option')[0], 'pages.admin.inventory.form.moveDown')
    await press(items(dialog, 'inventory-option')[1], 'pages.admin.inventory.form.moveUp')
    wrapper.findAllComponents(ElColorPicker)[1]?.vm.$emit('update:modelValue', '#FF0000')
    wrapper.findAllComponents(ElColorPicker)[0]?.vm.$emit('update:modelValue', null)

    await click(findButton(t('pages.admin.inventory.form.addColumn'), dialog))
    await typeIn(items(dialog, 'inventory-column')[1], 'inventory-column-name', 'Cantidad')
    selects()[1]?.vm.$emit('update:modelValue', 'Quantity')
    await click(findButton(t('pages.admin.inventory.form.addColumn'), dialog))
    await press(items(dialog, 'inventory-column')[2], 'pages.admin.inventory.form.removeColumn')
    await press(items(dialog, 'inventory-column')[1], 'pages.admin.inventory.form.moveUp')
    await press(items(dialog, 'inventory-column')[0], 'pages.admin.inventory.form.moveDown')
    await press(items(dialog, 'inventory-column')[1], 'pages.admin.inventory.form.moveUp')

    const quantityOption = selects()[1]
      ?.findAllComponents(ElOption)
      .find((option) => option.text() === t(columnTypeLabelKey('Quantity')))
    expect(quantityOption?.props('disabled')).toBe(true)
    expect(document.body.querySelector('.el-message-box')).toBeNull()

    await click(findButton(t('common.save'), dialog))

    expect(wrapper.emitted('submit')).toEqual([
      [
        {
          name: 'Camisetas',
          columns: [
            { id: null, name: 'Cantidad', type: 'Quantity', required: false, options: [] },
            {
              id: null,
              name: 'Talla',
              type: 'List',
              required: false,
              options: [
                { id: null, name: 'M', color: '#6366F1' },
                { id: null, name: 'S', color: '#FF0000' },
              ],
            },
          ],
        },
      ],
    ])
  })
})
