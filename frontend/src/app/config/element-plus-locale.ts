import es from 'element-plus/es/locale/lang/es'
import 'dayjs/locale/es'

/**
 * Element Plus Spanish locale with the strings its own Spanish file leaves in English, mostly
 * accessible labels of dialogs, tables, pagination and date pickers. Loading the Day.js Spanish
 * locale makes date pickers start their weeks on Monday.
 */
export const elementPlusLocale = {
  ...es,
  el: {
    ...es.el,
    breadcrumb: { ...es.el.breadcrumb, label: 'Ruta de navegación' },
    colorpicker: {
      ...es.el.colorpicker,
      defaultLabel: 'selector de color',
      description: 'el color actual es {color}. Pulsa Intro para elegir otro color.',
      alphaLabel: 'elegir la opacidad',
      alphaDescription: 'opacidad {alpha}, el color actual es {color}',
      hueLabel: 'elegir el tono',
      hueDescription: 'tono {hue}, el color actual es {color}',
      svLabel: 'elegir la saturación y el brillo',
      svDescription: 'saturación {saturation}, brillo {brightness}, el color actual es {color}',
      predefineDescription: 'elegir {value} como color',
    },
    datepicker: {
      ...es.el.datepicker,
      dateTablePrompt: 'Usa las flechas e Intro para elegir el día del mes',
      monthTablePrompt: 'Usa las flechas e Intro para elegir el mes',
      quarterTablePrompt: 'Usa las flechas e Intro para elegir el trimestre',
      yearTablePrompt: 'Usa las flechas e Intro para elegir el año',
      selectedDate: 'Fecha elegida',
      weeksFull: {
        sun: 'domingo',
        mon: 'lunes',
        tue: 'martes',
        wed: 'miércoles',
        thu: 'jueves',
        fri: 'viernes',
        sat: 'sábado',
      },
    },
    inputNumber: { ...es.el.inputNumber, decrease: 'restar', increase: 'sumar' },
    dropdown: { ...es.el.dropdown, toggleDropdown: 'Abrir o cerrar el menú' },
    pagination: {
      ...es.el.pagination,
      total: 'Total {total}',
      page: 'Página',
      prev: 'Ir a la página anterior',
      next: 'Ir a la página siguiente',
      currentPage: 'página {pager}',
      prevPages: '{pager} páginas anteriores',
      nextPages: '{pager} páginas siguientes',
    },
    dialog: { ...es.el.dialog, close: 'Cerrar este diálogo' },
    drawer: { ...es.el.drawer, close: 'Cerrar este panel' },
    messagebox: { ...es.el.messagebox, title: 'Mensaje', close: 'Cerrar este diálogo' },
    slider: {
      ...es.el.slider,
      defaultLabel: 'control deslizante entre {min} y {max}',
      defaultRangeStartLabel: 'elegir el valor inicial',
      defaultRangeEndLabel: 'elegir el valor final',
    },
    table: {
      ...es.el.table,
      selectAllLabel: 'Seleccionar todas las filas',
      selectRowLabel: 'Seleccionar esta fila',
      expandRowLabel: 'Desplegar esta fila',
      collapseRowLabel: 'Plegar esta fila',
      sortLabel: 'Ordenar por {column}',
      filterLabel: 'Filtrar por {column}',
    },
    tag: { ...es.el.tag, close: 'Quitar esta etiqueta' },
    tour: {
      ...es.el.tour,
      next: 'Siguiente',
      previous: 'Anterior',
      finish: 'Terminar',
      close: 'Cerrar este diálogo',
    },
    carousel: {
      ...es.el.carousel,
      leftArrow: 'Flecha izquierda del carrusel',
      rightArrow: 'Flecha derecha del carrusel',
      indicator: 'Ir a la diapositiva {index}',
    },
    inputOTP: {
      ...es.el.inputOTP,
      groupLabel: 'Código de un solo uso',
      defaultLabel: 'Escribe el carácter {index} del código',
    },
  },
}
