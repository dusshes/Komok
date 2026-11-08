import { useCallback, useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'

type Store = {
  id: number
  name: string
  currencyCode: string
  timeZoneId: string
  openingIntervals: { dayOfWeek: number; opensAt: string; closesAt: string }[]
}
type Product = {
  id: number
  categoryId: number
  category: string
  title: string
  description: string
  askingPrice: number
  publishedAt: string | null
  currencyCode: string
  status: string
  reservedUntil: string | null
}
type CatalogCategory = { id: number; name: string }
type Category = CatalogCategory & { isActive: boolean }
type CatalogResponse = { categories: CatalogCategory[]; products: Product[] }
type NoticeKind = 'success' | 'info' | 'error'
type Notice = { id: number; message: string; kind: NoticeKind }
type Listing = {
  id: number
  title: string
  description: string
  askingPrice: number
  storeId: number
  store: string
  listingStatus: string
  inventoryStatus: string
  reservedUntil: string | null
  arrivalAt: string | null
  timeZoneId: string
  soldPrice: number | null
  payoutAmount: number | null
  payoutStatus: string
  currency: string
}
type AdminProduct = {
  id: number
  categoryId: number
  category: string
  title: string
  description: string
  askingPrice: number
  commissionKind: string
  commissionRate: number | null
  listingStatus: string
  inventoryStatus: string
  reservedUntil: string | null
  timeZoneId: string
  sellerName: string | null
  sellerEmail: string
  sellerMessenger: string
  sellerMessengerContact: string
  sellerPayoutDetails: string
  arrivalAt: string | null
  buyerName: string | null
  buyerMessenger: string | null
  buyerMessengerContact: string | null
  soldPrice: number | null
  payoutAmount: number | null
  payoutStatus: string
  currencyCode: string
}
type OpeningInterval = { dayOfWeek: number; opensAt: string; closesAt: string }
type CommissionBand = { priceThreshold: number; kind: 'Percent' | 'Fixed'; rate: number }
type ManagedStore = Store & {
  address: string
  timeZoneId: string
  bookingDurationHours: number
  isActive: boolean
  openingIntervals: OpeningInterval[]
  commissionBands: CommissionBand[]
}
type BookingSlot = { value: string; label: string }
type CurrentUser = {
  name: string | null
  email: string
  messenger: string
  messengerContact: string
  isSeller: boolean
  isStoreAdmin: boolean
  isSuperAdmin: boolean
  storeIds: number[]
}

const russianCollator = new Intl.Collator('ru-RU', { sensitivity: 'base', numeric: true })
const dayNames = ['Воскресенье', 'Понедельник', 'Вторник', 'Среда', 'Четверг', 'Пятница', 'Суббота']
const listingLabels: Record<string, string> = {
  Submitted: 'На проверке',
  NeedsRevision: 'Нужна доработка',
  Rejected: 'Отклонена',
  AwaitingDelivery: 'Ожидает доставки',
  Delivery: 'Доставляется',
  OnSale: 'На продаже',
  Cancelled: 'Отменена',
}

async function api<T>(url: string, init?: RequestInit): Promise<T> {
  let response: Response
  try {
    response = await fetch(url, { credentials: 'same-origin', ...init })
  } catch {
    throw new RequestError('Не удалось связаться с сервером. Проверьте, что приложение запущено, и повторите попытку.')
  }
  if (!response.ok) {
    let body: { message?: string; detail?: string; errors?: Record<string, string[] | string> | string[] } = {}
    try {
      body = await response.json() as typeof body
    } catch { /* Ответ может быть без JSON. */ }
    const fieldErrors: Record<string, string[]> = {}
    if (body.errors && !Array.isArray(body.errors)) {
      for (const [field, messages] of Object.entries(body.errors)) {
        const clientField = field.split('.').map((part) => part.charAt(0).toLowerCase() + part.slice(1)).join('.')
        const fieldMessages = Array.isArray(messages) ? messages : [messages]
        fieldErrors[clientField] = fieldMessages.map((message) =>
          /[А-Яа-яЁё]/.test(message) ? message : 'Проверьте значение этого поля.')
      }
    }
    const defaultMessage: Record<number, string> = {
      400: 'Проверьте введённые данные.',
      401: 'Неверный email или пароль.',
      403: 'У вас недостаточно прав для этого действия.',
      404: 'Запрашиваемая запись не найдена.',
      409: 'Данные изменились. Обновите страницу и повторите действие.',
      503: 'Сервис временно недоступен. Попробуйте позже.',
    }
    const serverMessage = body.message ?? body.detail
    const message = serverMessage && /[А-Яа-яЁё]/.test(serverMessage)
      ? serverMessage
      : defaultMessage[response.status] ?? 'Не удалось выполнить запрос.'
    throw new RequestError(message, fieldErrors)
  }
  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

class RequestError extends Error {
  constructor(message: string, readonly fieldErrors: Record<string, string[]> = {}) {
    super(message)
  }
}

type FormControl = HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement

function validationMessage(control: FormControl): string {
  const validity = control.validity
  if (validity.valueMissing) return `Заполните поле «${fieldLabel(control)}».`
  if (validity.typeMismatch && control instanceof HTMLInputElement && control.type === 'email')
    return 'Введите email в формате name@example.com.'
  if (validity.tooShort && control instanceof HTMLInputElement)
    return `Введите не менее ${control.minLength} символов.`
  if (validity.tooLong && control instanceof HTMLInputElement)
    return `Введите не более ${control.maxLength} символов.`
  if (validity.rangeUnderflow && control instanceof HTMLInputElement)
    return `Укажите значение не меньше ${control.min}.`
  if (validity.rangeOverflow && control instanceof HTMLInputElement)
    return `Укажите значение не больше ${control.max}.`
  if (validity.stepMismatch && control instanceof HTMLInputElement)
    return `Укажите значение с шагом ${control.step}.`
  if (validity.badInput && control instanceof HTMLInputElement && control.type === 'number')
    return 'Введите число в правильном формате.'
  if (validity.patternMismatch) return `Проверьте формат поля «${fieldLabel(control)}».`
  return `Проверьте поле «${fieldLabel(control)}».`
}

function fieldLabel(control: FormControl): string {
  const label = control.closest('label')
  if (label) {
    const copy = label.cloneNode(true) as HTMLLabelElement
    copy.querySelectorAll('input, textarea, select, .field-error').forEach((item) => item.remove())
    const text = copy.textContent?.trim()
    if (text) return text
  }
  const placeholder = control instanceof HTMLInputElement || control instanceof HTMLTextAreaElement
    ? control.placeholder
    : null
  return control.getAttribute('aria-label') ?? placeholder ?? 'это поле'
}

function setFieldError(control: FormControl, message: string) {
  const name = control.name || control.id || fieldLabel(control)
  const container = control.closest('label') ?? control.parentElement
  if (!container) return
  let error = container.querySelector<HTMLElement>(`.field-error[data-field="${CSS.escape(name)}"]`)
  if (!error) {
    error = document.createElement('span')
    error.className = 'field-error'
    error.id = `field-error-${Math.random().toString(36).slice(2)}`
    control.insertAdjacentElement('afterend', error)
  }
  error.dataset.field = name
  error.textContent = message
  control.setAttribute('aria-invalid', 'true')
  const describedBy = (control.getAttribute('aria-describedby') ?? '').split(/\s+/).filter(Boolean)
  if (!describedBy.includes(error.id)) describedBy.push(error.id)
  control.setAttribute('aria-describedby', describedBy.join(' '))
}

function clearFieldError(form: HTMLFormElement, control: FormControl) {
  const name = control.name || control.id || fieldLabel(control)
  const container = control.closest('label') ?? control.parentElement
  container?.querySelectorAll<HTMLElement>('.field-error').forEach((error) => {
    if (error.dataset.field !== name) return
    error.remove()
    const describedBy = (control.getAttribute('aria-describedby') ?? '').split(/\s+/).filter((id) => id !== error.id)
    if (describedBy.length) control.setAttribute('aria-describedby', describedBy.join(' '))
    else control.removeAttribute('aria-describedby')
  })
  control.removeAttribute('aria-invalid')
  form.querySelector('.form-error')?.remove()
}

function handleInvalidField(event: FormEvent<HTMLFormElement>) {
  const control = event.target
  if (!(control instanceof HTMLInputElement || control instanceof HTMLTextAreaElement || control instanceof HTMLSelectElement)) return
  event.preventDefault()
  setFieldError(control, validationMessage(control))
}

function handleFormInput(event: FormEvent<HTMLFormElement>) {
  const control = event.target
  if (control instanceof HTMLInputElement || control instanceof HTMLTextAreaElement || control instanceof HTMLSelectElement)
    clearFieldError(event.currentTarget, control)
}

function showFormFailure(form: HTMLFormElement, error: unknown, fallback: string) {
  const removedErrorIds = [...form.querySelectorAll<HTMLElement>('.field-error')].map((item) => item.id)
  form.querySelectorAll<FormControl>('[aria-describedby]').forEach((control) => {
    const describedBy = (control.getAttribute('aria-describedby') ?? '').split(/\s+/).filter((id) => id && !removedErrorIds.includes(id))
    if (describedBy.length) control.setAttribute('aria-describedby', describedBy.join(' '))
    else control.removeAttribute('aria-describedby')
  })
  form.querySelectorAll('.field-error, .form-error').forEach((item) => item.remove())
  form.querySelectorAll('[aria-invalid="true"]').forEach((item) => item.removeAttribute('aria-invalid'))
  const fieldErrors = error instanceof RequestError ? error.fieldErrors : {}
  const unplaced: string[] = []
  for (const [name, messages] of Object.entries(fieldErrors)) {
    const control = form.querySelector<FormControl>(`[name="${CSS.escape(name)}"]`)
    if (control) setFieldError(control, messages.join(' '))
    else unplaced.push(...messages)
  }
  const message = unplaced.length
    ? unplaced.join(' ')
    : error instanceof Error && /[А-Яа-яЁё]/.test(error.message) ? error.message : fallback
  if (message && (!Object.keys(fieldErrors).length || unplaced.length)) {
    const general = document.createElement('div')
    general.className = 'form-error'
    general.setAttribute('role', 'alert')
    general.textContent = message
    form.prepend(general)
  }
}

function makeStoreForm(): {
  name: string
  address: string
  currencyCode: string
  timeZoneId: string
  bookingDurationHours: number
  openingIntervals: OpeningInterval[]
  commissionBands: CommissionBand[]
} {
  return {
    name: '',
    address: '',
    currencyCode: 'RUB',
    timeZoneId: '+03:00',
    bookingDurationHours: 24,
    openingIntervals: [1, 2, 3, 4, 5, 6].map((dayOfWeek) => ({ dayOfWeek, opensAt: '10:00', closesAt: '19:00' })),
    commissionBands: [{ priceThreshold: 0, kind: 'Percent' as const, rate: 30 }],
  }
}

export default function App() {
  const [stores, setStores] = useState<Store[]>([])
  const [storeId, setStoreId] = useState<number | null>(null)
  const [products, setProducts] = useState<Product[]>([])
  const [catalogCategories, setCatalogCategories] = useState<CatalogCategory[]>([])
  const [activeCategories, setActiveCategories] = useState<Category[]>([])
  const [selectedCategoryIds, setSelectedCategoryIds] = useState<number[]>([])
  const [catalogSearch, setCatalogSearch] = useState('')
  const [catalogSort, setCatalogSort] = useState<'newest' | 'alphabetical' | 'price-asc' | 'price-desc'>('newest')
  const [groupByCategory, setGroupByCategory] = useState(false)
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [view, setView] = useState<'catalog' | 'account' | 'listings' | 'new-listing' | 'admin'>('catalog')
  const [notice, setNotice] = useState<Notice | null>(null)
  const noticeTimer = useRef<number | null>(null)
  const noticeId = useRef(0)
  const [loadingCatalog, setLoadingCatalog] = useState(false)
  const [authMode, setAuthMode] = useState<'login' | 'register'>('login')
  const [authBusy, setAuthBusy] = useState(false)
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [messenger, setMessenger] = useState('Zalo')
  const [messengerContact, setMessengerContact] = useState('')
  const [payoutDetails, setPayoutDetails] = useState('')
  const [listings, setListings] = useState<Listing[]>([])
  const [relativeClock, setRelativeClock] = useState(() => Date.now())
  const [listingForm, setListingForm] = useState({ title: '', description: '', askingPrice: '', categoryId: '' })
  const [listingBusy, setListingBusy] = useState(false)
  const [bookingProduct, setBookingProduct] = useState<Product | null>(null)
  const [booking, setBooking] = useState({ arrivalAt: '', name: '', messenger: 'Zalo', messengerContact: '' })
  const [bookingAddress, setBookingAddress] = useState('')
  const [bookingSlots, setBookingSlots] = useState<BookingSlot[]>([])
  const [bookingLoading, setBookingLoading] = useState(false)
  const [adminStores, setAdminStores] = useState<ManagedStore[]>([])
  const [adminStoreId, setAdminStoreId] = useState<number | null>(null)
  const [adminProducts, setAdminProducts] = useState<AdminProduct[]>([])
  const [adminCategories, setAdminCategories] = useState<Category[]>([])
  const [newCategoryName, setNewCategoryName] = useState('')
  const [categoryBusy, setCategoryBusy] = useState(false)
  const [storeForm, setStoreForm] = useState(makeStoreForm)
  const [storeBusy, setStoreBusy] = useState(false)
  const [adminEmail, setAdminEmail] = useState('')
  const [adminStoreIds, setAdminStoreIds] = useState<number[]>([])

  const clearNotice = useCallback(() => {
    if (noticeTimer.current !== null) window.clearTimeout(noticeTimer.current)
    noticeTimer.current = null
    setNotice(null)
  }, [])

  const showNotice = useCallback((message: string, kind: NoticeKind) => {
    if (noticeTimer.current !== null) window.clearTimeout(noticeTimer.current)
    noticeTimer.current = null
    const id = ++noticeId.current
    setNotice({ id, message, kind })
    if (kind !== 'error') {
      const timer = window.setTimeout(() => {
        setNotice((current) => current?.id === id ? null : current)
        if (noticeTimer.current === timer) noticeTimer.current = null
      }, 30_000)
      noticeTimer.current = timer
    }
  }, [])

  useEffect(() => () => {
    if (noticeTimer.current !== null) window.clearTimeout(noticeTimer.current)
  }, [])

  const loadCatalog = useCallback(async (id: number) => {
    setLoadingCatalog(true)
    try {
      const catalog = await api<CatalogResponse>(`/api/catalog?storeId=${id}`)
      setProducts(catalog.products)
      setCatalogCategories(catalog.categories)
      setSelectedCategoryIds((current) => current.filter((categoryId) => catalog.categories.some((category) => category.id === categoryId)))
    } catch (error) {
      showNotice(error instanceof Error ? error.message : 'Не удалось загрузить каталог.', 'error')
    } finally {
      setLoadingCatalog(false)
    }
  }, [])

  const loadActiveCategories = useCallback(async () => {
    try { setActiveCategories(await api<Category[]>('/api/categories/active')) }
    catch (error) { showNotice(error instanceof Error ? error.message : 'Не удалось загрузить категории.', 'error') }
  }, [])

  const loadAdminCategories = useCallback(async () => {
    try { setAdminCategories(await api<Category[]>('/api/admin/categories')) }
    catch (error) { showNotice(error instanceof Error ? error.message : 'Не удалось загрузить категории.', 'error') }
  }, [])

  const loadAccount = useCallback(async () => {
    try {
      const current = await api<CurrentUser>('/api/auth/me')
      setUser(current)
      return current
    } catch {
      setUser(null)
      return null
    }
  }, [])

  const loadListings = useCallback(async () => {
    try { setListings(await api<Listing[]>('/api/listings/mine')) }
    catch (error) { showNotice(error instanceof Error ? error.message : 'Не удалось загрузить заявки.', 'error') }
  }, [])

  const loadAdminStores = useCallback(async (preferredId?: number) => {
    try {
      const data = await api<ManagedStore[]>('/api/admin/stores')
      setAdminStores(data)
      if (data.length > 0) {
        const selected = data.find((store) => store.id === (preferredId ?? adminStoreId)) ?? data[0]
        setAdminStoreId(selected.id)
        setAdminStoreIds((current) => current.length > 0 ? current : [selected.id])
        setStoreForm({
          name: selected.name,
          address: selected.address,
          currencyCode: selected.currencyCode,
          timeZoneId: selected.timeZoneId,
          bookingDurationHours: selected.bookingDurationHours,
          openingIntervals: selected.openingIntervals.map((interval) => ({
            dayOfWeek: interval.dayOfWeek,
            opensAt: interval.opensAt.slice(0, 5),
            closesAt: interval.closesAt.slice(0, 5),
          })),
          commissionBands: selected.commissionBands.map((band) => ({ ...band })),
        })
        await loadAdminProducts(selected.id)
      }
    } catch (error) {
      showNotice(error instanceof Error ? error.message : 'Не удалось загрузить магазины.', 'error')
    }
  // loadAdminProducts is stable and selected store is used only to preserve the current choice.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [adminStoreId])

  async function loadAdminProducts(id: number) {
    try { setAdminProducts(await api<AdminProduct[]>(`/api/admin/products?storeId=${id}`)) }
    catch (error) { showNotice(error instanceof Error ? error.message : 'Не удалось загрузить товары магазина.', 'error') }
  }

  useEffect(() => {
    void (async () => {
      const params = new URLSearchParams(window.location.search)
      const confirmEmail = params.get('confirmEmail')
      const token = params.get('token')
      if (confirmEmail && token) {
        try {
          const result = await api<{ message: string }>('/api/auth/confirm-email', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: confirmEmail, token }),
          })
          showNotice(result.message, 'success')
          setView('account')
        } catch (error) {
          showNotice(error instanceof Error ? error.message : 'Не удалось подтвердить email.', 'error')
          setView('account')
        }
        window.history.replaceState({}, '', window.location.pathname)
      }

      await loadActiveCategories()
      const currentStores = await api<Store[]>('/api/stores').catch(() => [] as Store[])
      setStores(currentStores)
      if (currentStores.length > 0) {
        setStoreId(currentStores[0].id)
        await loadCatalog(currentStores[0].id)
      }
      const currentUser = await loadAccount()
      if (currentUser) {
        await loadListings()
        if (currentUser.isSuperAdmin) await loadAdminCategories()
        if (currentUser.isStoreAdmin || currentUser.isSuperAdmin) await loadAdminStores()
      }
    })()
  // Initial load only.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    if (view !== 'listings') return

    let timer: number | undefined
    const updateRelativeClock = () => {
      const now = Date.now()
      setRelativeClock(now)

      const nextArrival = listings
        .map((listing) => listing.arrivalAt ? Date.parse(listing.arrivalAt) : Number.NaN)
        .filter((arrivalAt) => Number.isFinite(arrivalAt) && arrivalAt > now)
        .sort((left, right) => left - right)[0]

      if (nextArrival === undefined) return

      const nextMinute = 60_000 - (now % 60_000)
      timer = window.setTimeout(updateRelativeClock, Math.min(nextMinute, nextArrival - now))
    }

    updateRelativeClock()
    return () => { if (timer !== undefined) window.clearTimeout(timer) }
  }, [view, listings])

  async function submitAuth(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    setAuthBusy(true)
    try {
      if (authMode === 'register') {
        const result = await api<{ message: string }>('/api/auth/register', {
          method: 'POST', headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ name, email, password, confirmPassword, messenger, messengerContact, payoutDetails }),
        })
        showNotice(result.message, 'info')
        return
      }
      await api('/api/auth/login', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password, rememberMe: true }),
      })
      const current = await loadAccount()
      if (current) {
        await loadListings()
        if (current.isSuperAdmin) await loadAdminCategories()
        if (current.isStoreAdmin || current.isSuperAdmin) await loadAdminStores()
        setView(current.isStoreAdmin || current.isSuperAdmin ? 'admin' : 'listings')
      }
      showNotice('Вы вошли в аккаунт.', 'success')
    } catch (error) {
      showFormFailure(form, error, 'Не удалось выполнить запрос.')
    } finally {
      setAuthBusy(false)
    }
  }

  async function logout() {
    try { await api('/api/auth/logout', { method: 'POST' }) } catch { /* Сбрасываем локальное состояние в любом случае. */ }
    setUser(null)
    setListings([])
    setAdminStores([])
    setView('catalog')
    showNotice('Вы вышли из аккаунта.', 'success')
  }

  async function createListing(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    if (!storeId) return
    setListingBusy(true)
    try {
      await api('/api/listings', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ...listingForm, storeId, categoryId: Number(listingForm.categoryId), askingPrice: Number(listingForm.askingPrice) }),
      })
      setListingForm({ title: '', description: '', askingPrice: '', categoryId: '' })
      showNotice('Заявка отправлена администратору магазина.', 'success')
      await loadListings()
      setView('listings')
    } catch (error) {
      showFormFailure(form, error, 'Не удалось отправить заявку.')
    } finally {
      setListingBusy(false)
    }
  }

  async function createCategory(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    setCategoryBusy(true)
    try {
      await api('/api/admin/categories', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: newCategoryName }),
      })
      setNewCategoryName('')
      await Promise.all([loadAdminCategories(), loadActiveCategories()])
      if (storeId) await loadCatalog(storeId)
      showNotice('Категория добавлена.', 'success')
    } catch (error) {
      showFormFailure(form, error, 'Не удалось добавить категорию.')
    } finally {
      setCategoryBusy(false)
    }
  }

  async function createBooking(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    if (!bookingProduct || !booking.arrivalAt) return
    try {
      await api('/api/bookings', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          productId: bookingProduct.id,
          arrivalAt: booking.arrivalAt,
          name: booking.name || null,
          messenger: booking.messenger,
          messengerContact: booking.messengerContact,
        }),
      })
      showNotice('Товар забронирован. Сохраните выбранное время визита.', 'success')
      setBookingProduct(null)
      setBooking({ arrivalAt: '', name: '', messenger: 'Zalo', messengerContact: '' })
      setBookingSlots([])
      setBookingAddress('')
      if (storeId) await loadCatalog(storeId)
    } catch (error) {
      showFormFailure(form, error, 'Не удалось забронировать товар.')
    }
  }

  async function openBooking(product: Product) {
    setBookingProduct(product)
    setBooking({ arrivalAt: '', name: '', messenger: 'Zalo', messengerContact: '' })
    setBookingSlots([])
    setBookingAddress('')
    setBookingLoading(true)
    try {
      const options = await api<{ address: string; slots: BookingSlot[] }>(`/api/products/${product.id}/booking-options`)
      setBookingAddress(options.address)
      setBookingSlots(options.slots)
    } catch (error) {
      setBookingProduct(null)
      showNotice(error instanceof Error ? error.message : 'Не удалось загрузить время визита.', 'error')
    } finally {
      setBookingLoading(false)
    }
  }

  async function listingAction(id: number, action: 'delivery' | 'cancel') {
    try {
      await api(`/api/listings/${id}/${action}`, { method: 'POST' })
      await loadListings()
      showNotice(action === 'delivery' ? 'Отметили передачу товара для доставки.' : 'Заявка отменена.', 'success')
    } catch (error) { showNotice(error instanceof Error ? error.message : 'Не удалось изменить заявку.', 'error') }
  }

  async function saveStore(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    setStoreBusy(true)
    const payload = {
      ...storeForm,
      openingIntervals: storeForm.openingIntervals.map((interval) => ({
        ...interval, opensAt: `${interval.opensAt}:00`, closesAt: `${interval.closesAt}:00`,
      })),
    }
    try {
      let savedStoreId = adminStoreId
      if (adminStoreId) {
        await api(`/api/admin/stores/${adminStoreId}`, {
          method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload),
        })
      } else {
        const created = await api<{ id: number }>('/api/admin/stores', {
          method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload),
        })
        savedStoreId = created.id
      }
      showNotice('Настройки магазина сохранены.', 'success')
      await loadAdminStores(savedStoreId ?? undefined)
      const publicStores = await api<Store[]>('/api/stores')
      setStores(publicStores)
    } catch (error) { showFormFailure(form, error, 'Не удалось сохранить магазин.') }
    finally { setStoreBusy(false) }
  }

  async function assignAdmin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    try {
      await api('/api/admin/users/assign-admin', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: adminEmail, storeIds: adminStoreIds }),
      })
      showNotice('Администратор назначен выбранным магазинам.', 'success')
      setAdminEmail('')
    } catch (error) { showFormFailure(form, error, 'Не удалось назначить администратора.') }
  }

  function setIntervalValue(index: number, key: keyof OpeningInterval, value: string | number) {
    setStoreForm((current) => ({
      ...current,
      openingIntervals: current.openingIntervals.map((interval, itemIndex) =>
        itemIndex === index ? { ...interval, [key]: value } : interval),
    }))
  }

  function setBandValue(index: number, key: keyof CommissionBand, value: string | number) {
    setStoreForm((current) => ({
      ...current,
      commissionBands: current.commissionBands.map((band, itemIndex) =>
        itemIndex === index ? { ...band, [key]: key === 'kind' ? value : Number(value) } : band),
    }))
  }

  function selectManagedStore(id: number) {
    setAdminStoreId(id)
    const store = adminStores.find((item) => item.id === id)
    if (store) {
      setStoreForm({
        name: store.name,
        address: store.address,
        currencyCode: store.currencyCode,
        timeZoneId: store.timeZoneId,
        bookingDurationHours: store.bookingDurationHours,
        openingIntervals: store.openingIntervals.map((interval) => ({
          dayOfWeek: interval.dayOfWeek,
          opensAt: interval.opensAt.slice(0, 5),
          closesAt: interval.closesAt.slice(0, 5),
        })),
        commissionBands: store.commissionBands.map((band) => ({ ...band })),
      })
    }
  }

  const catalogTimeZoneId = stores.find((store) => store.id === storeId)?.timeZoneId ?? '+00:00'
  const sortedCatalogCategories = [...catalogCategories].sort((left, right) => russianCollator.compare(left.name, right.name))
  const normalizedSearch = catalogSearch.trim().toLocaleLowerCase('ru-RU')
  const selectedCategories = new Set(selectedCategoryIds)
  const visibleProducts = products
    .filter((product) => selectedCategories.size === 0 || selectedCategories.has(product.categoryId))
    .filter((product) => !normalizedSearch || `${product.title}\n${product.description}`.toLocaleLowerCase('ru-RU').includes(normalizedSearch))
    .sort((left, right) => {
      const titleOrder = russianCollator.compare(left.title, right.title)
      if (catalogSort === 'alphabetical') return titleOrder || left.id - right.id
      if (catalogSort === 'price-asc') return left.askingPrice - right.askingPrice || titleOrder || left.id - right.id
      if (catalogSort === 'price-desc') return right.askingPrice - left.askingPrice || titleOrder || left.id - right.id
      const leftPublished = left.publishedAt ? Date.parse(left.publishedAt) : 0
      const rightPublished = right.publishedAt ? Date.parse(right.publishedAt) : 0
      return (Number.isFinite(rightPublished) ? rightPublished : 0) - (Number.isFinite(leftPublished) ? leftPublished : 0)
        || titleOrder || left.id - right.id
    })
  const categoryGroups = sortedCatalogCategories.map((category) => ({
    category,
    products: visibleProducts.filter((product) => product.categoryId === category.id),
  })).filter((group) => group.products.length > 0)

  function renderProductCard(product: Product) {
    return <article className="product-card" key={product.id}>
      <div className="product-visual"><span className="product-category">{product.category}</span><span className="product-symbol">✳</span></div>
      <div className="product-info"><h3>{product.title}</h3><p>{product.description}</p><div className="product-bottom"><strong>{formatMoney(product.askingPrice, product.currencyCode)}</strong>
        {product.status === 'Available' ? <button className="button button-small" onClick={() => void openBooking(product)}>Забронировать</button> : product.status === 'Reserved' ? <span className="badge badge-warm">Забронировано до {formatDate(product.reservedUntil, catalogTimeZoneId)}</span> : <span className="badge">Продано</span>}
      </div>
      {bookingProduct?.id === product.id && <form className="booking-form" onSubmit={createBooking} onInvalidCapture={handleInvalidField} onInputCapture={handleFormInput} onChangeCapture={handleFormInput}>
        <h4>Когда вы приедете?</h4>
        {bookingLoading ? <p className="muted">Загружаем доступное время…</p> : <label>Дата и время визита<select name="arrivalAt" required value={booking.arrivalAt} onChange={(event) => setBooking({ ...booking, arrivalAt: event.target.value })}><option value="">Выберите доступное время</option>{bookingSlots.map((slot) => <option key={slot.value} value={slot.value}>{slot.label}</option>)}</select></label>}
        {!bookingLoading && bookingSlots.length === 0 && <p className="muted">Нет доступного времени для визита в срок брони.</p>}
        {bookingAddress && <p className="muted">Я приеду на осмотр по адресу: {bookingAddress}</p>}
        <label>Мессенджер<select name="messenger" required value={booking.messenger} onChange={(event) => setBooking({ ...booking, messenger: event.target.value })}><option>Telegram</option><option>WhatsApp</option><option>MAX</option><option>Zalo</option></select></label>
        <label>Контакт в мессенджере<input name="messengerContact" required maxLength={200} value={booking.messengerContact} onChange={(event) => setBooking({ ...booking, messengerContact: event.target.value })} /></label>
        <label>Имя (необязательно)<input name="name" maxLength={160} value={booking.name} onChange={(event) => setBooking({ ...booking, name: event.target.value })} /></label>
        <div className="button-row"><button className="button button-small" type="submit">Подтвердить бронь</button><button className="button-quiet" type="button" onClick={() => setBookingProduct(null)}>Отмена</button></div>
      </form>}
      </div>
    </article>
  }

  return (
    <main className="site-shell">
      <header className="topbar">
        <a className="brand" href="#catalog" onClick={() => setView('catalog')}><span className="brand-mark">К</span><span>Комок<small>комиссионные магазины</small></span></a>
        <nav className="navigation" aria-label="Основная навигация">
          <button className={view === 'catalog' ? 'nav-link active' : 'nav-link'} onClick={() => setView('catalog')}>Каталог</button>
          {user && <button className={view === 'listings' ? 'nav-link active' : 'nav-link'} onClick={() => { setView('listings'); void loadListings() }}>Мои товары</button>}
          {(user?.isStoreAdmin || user?.isSuperAdmin) && <button className={view === 'admin' ? 'nav-link active' : 'nav-link'} onClick={() => { setView('admin'); void loadAdminStores() }}>Управление</button>}
          <button className={view === 'account' ? 'nav-link active' : 'nav-link'} onClick={() => setView('account')}>{user ? user.email : 'Войти'}</button>
        </nav>
      </header>

      {notice && <div className={`notice notice-${notice.kind}`} role={notice.kind === 'error' ? 'alert' : 'status'}><span>{notice.message}</span><button aria-label="Закрыть уведомление" onClick={clearNotice}>×</button></div>}

      {view === 'catalog' && <section className="page-section">
        <div className="hero">
          <div><p className="eyebrow">Хорошие вещи находят новый дом</p><h1>Вещи с историей.<br /><em>Новые владельцы.</em></h1><p>Выберите магазин, найдите нужное и забронируйте для визита.</p></div>
          <div className="hero-art" aria-hidden="true"><div className="sun" /><div className="vase vase-one" /><div className="vase vase-two" /><div className="book book-one" /><div className="book book-two" /></div>
        </div>

        <div className="section-heading"><div><p className="eyebrow">Подборка магазина</p><h2>Каталог</h2></div>
          <label className="store-filter">Магазин<select value={storeId ?? ''} onChange={(event) => { const id = Number(event.target.value); setStoreId(id); void loadCatalog(id) }}>
            {stores.length === 0 && <option value="">Пока нет магазинов</option>}
            {stores.map((store) => <option key={store.id} value={store.id}>{store.name}</option>)}
          </select></label>
        </div>
        {storeId !== null && <div className="catalog-controls">
          <label className="catalog-search">Поиск<input aria-label="Поиск по названию и описанию" placeholder="Название или описание" value={catalogSearch} onChange={(event) => setCatalogSearch(event.target.value)} /></label>
          <details className="category-filter"><summary>{selectedCategoryIds.length === 0 ? 'Все категории' : `Категории: ${selectedCategoryIds.length}`}</summary>
            <div className="category-options">{sortedCatalogCategories.map((category) => <label className="check-row" key={category.id}>
              <input type="checkbox" checked={selectedCategoryIds.includes(category.id)} onChange={(event) => setSelectedCategoryIds((current) => event.target.checked ? [...current, category.id] : current.filter((id) => id !== category.id))} />{category.name}
            </label>)}</div>
          </details>
          <label className="catalog-sort">Сортировка<select value={catalogSort} onChange={(event) => setCatalogSort(event.target.value as typeof catalogSort)}>
            <option value="newest">Сначала новые</option><option value="alphabetical">По алфавиту</option><option value="price-asc">Сначала дешевле</option><option value="price-desc">Сначала дороже</option>
          </select></label>
          <label className="check-row catalog-grouping"><input type="checkbox" checked={groupByCategory} onChange={(event) => setGroupByCategory(event.target.checked)} />Группировать по категориям</label>
          {(selectedCategoryIds.length > 0 || catalogSearch) && <button className="button-quiet catalog-reset" type="button" onClick={() => { setSelectedCategoryIds([]); setCatalogSearch('') }}>Сбросить фильтры</button>}
        </div>}
        {loadingCatalog ? <p className="empty-state">Загружаем товары…</p> : products.length === 0 ? <div className="empty-state"><span>✳</span><h3>Пока нет товаров на витрине</h3><p>Загляните позже или выберите другой магазин.</p></div> : visibleProducts.length === 0 ? <div className="empty-state"><h3>Ничего не найдено</h3><p>Измените условия поиска или фильтры.</p><button className="button button-outline" onClick={() => { setSelectedCategoryIds([]); setCatalogSearch('') }}>Сбросить фильтры</button></div> : groupByCategory ?
          <div className="catalog-groups">{categoryGroups.map(({ category, products: groupProducts }) => <section className="category-group" key={category.id}><h3>{category.name}</h3><div className="product-grid">{groupProducts.map(renderProductCard)}</div></section>)}</div> :
          <div className="product-grid">{visibleProducts.map(renderProductCard)}</div>}
      </section>}

      {view === 'account' && <section className="narrow-section">
        <p className="eyebrow">Личный кабинет</p>
        <h1>{user ? 'Ваш аккаунт' : authMode === 'login' ? 'С возвращением' : 'Стать продавцом'}</h1>
        {user ? <div className="panel">{user.name && <p className="signed-name">{user.name}</p>}<p className="signed-email">{user.email}</p><p>{user.isSuperAdmin ? 'Главный администратор' : user.isStoreAdmin ? 'Администратор магазина' : 'Продавец'}</p><p className="muted">{user.messenger}: {user.messengerContact}</p><button className="button button-outline" onClick={() => void logout()}>Выйти</button></div> : <>
          <div className="segmented"><button className={authMode === 'login' ? 'selected' : ''} onClick={() => setAuthMode('login')}>Войти</button><button className={authMode === 'register' ? 'selected' : ''} onClick={() => setAuthMode('register')}>Регистрация продавца</button></div>
          <form className="panel form-stack" onSubmit={submitAuth} onInvalidCapture={handleInvalidField} onInputCapture={handleFormInput} onChangeCapture={handleFormInput}>
            <label>Email<input name="email" type="email" autoComplete="email" required maxLength={256} value={email} onChange={(event) => setEmail(event.target.value)} /></label>
            <label>Пароль<input name="password" type="password" autoComplete={authMode === 'login' ? 'current-password' : 'new-password'} required minLength={10} maxLength={128} value={password} onChange={(event) => setPassword(event.target.value)} /></label>
            {authMode === 'register' && <>
              <label>Имя<input name="name" autoComplete="name" required maxLength={160} value={name} onChange={(event) => setName(event.target.value)} /></label>
              <label>Повторите пароль<input name="confirmPassword" type="password" autoComplete="new-password" required minLength={10} maxLength={128} value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} /></label>
              <label>Мессенджер<select name="messenger" required value={messenger} onChange={(event) => setMessenger(event.target.value)}><option>Telegram</option><option>WhatsApp</option><option>MAX</option><option>Zalo</option></select></label>
              <label>Контакт в мессенджере<input name="messengerContact" required maxLength={200} value={messengerContact} onChange={(event) => setMessengerContact(event.target.value)} /></label>
              <label>Реквизиты для выплаты (необязательно)<textarea name="payoutDetails" maxLength={1000} rows={3} value={payoutDetails} onChange={(event) => setPayoutDetails(event.target.value)} /></label>
              <p className="muted">После регистрации подтвердите email по ссылке из письма.</p>
            </>}
            <button className="button" disabled={authBusy}>{authBusy ? 'Подождите…' : authMode === 'login' ? 'Войти' : 'Создать аккаунт'}</button>
          </form>
        </>}
      </section>}

      {view === 'listings' && <section className="page-section">
        <div className="section-heading"><div><p className="eyebrow">Для продавцов</p><h1>Мои товары</h1></div><button className="button" onClick={() => setView('new-listing')}>＋ Подать заявку</button></div>
        {listings.length === 0 ? <div className="empty-state"><span>＋</span><h3>Заявок пока нет</h3><p>Подайте заявку, чтобы предложить вещь магазину.</p><button className="button" onClick={() => setView('new-listing')}>Подать заявку</button></div> : <div className="listing-list">{listings.map((listing) => <article className="listing-row" key={listing.id}>
          <div><span className="badge">{listingLabels[listing.listingStatus] ?? listing.listingStatus}</span><h3>{listing.title}</h3><p>{listing.store} · {formatMoney(listing.askingPrice, listing.currency)}</p>
            {(listing.reservedUntil || listing.arrivalAt) && <div className="reservation-details">{listing.reservedUntil && <p className="muted">Бронь до {formatDate(listing.reservedUntil, listing.timeZoneId)}</p>}{listing.arrivalAt && <p className="muted">Прибытие на осмотр {formatDate(listing.arrivalAt, listing.timeZoneId)} ({formatRelativeArrival(listing.arrivalAt, relativeClock)})</p>}</div>}{listing.payoutAmount !== null && <p className="muted">К выплате: {formatMoney(listing.payoutAmount, listing.currency)} · {listing.payoutStatus === 'Paid' ? 'выплачено' : 'ожидает выплаты'}</p>}
          </div>
          <div className="button-row">{listing.listingStatus === 'AwaitingDelivery' && <><button className="button button-small" onClick={() => void listingAction(listing.id, 'delivery')}>Передал в доставку</button><button className="button-quiet" onClick={() => void listingAction(listing.id, 'cancel')}>Отменить заявку</button></>}</div>
        </article>)}</div>}
      </section>}

      {view === 'new-listing' && <section className="narrow-section"><p className="eyebrow">Предложить вещь</p><h1>Заявка на комиссию</h1><form className="panel form-stack" onSubmit={createListing} onInvalidCapture={handleInvalidField} onInputCapture={handleFormInput} onChangeCapture={handleFormInput}>
        <label>Магазин<select name="storeId" required value={storeId ?? ''} onChange={(event) => setStoreId(Number(event.target.value))}>{stores.map((store) => <option key={store.id} value={store.id}>{store.name}</option>)}</select></label>
        <label>Категория<select name="categoryId" required value={listingForm.categoryId} onChange={(event) => setListingForm({ ...listingForm, categoryId: event.target.value })}><option value="">Выберите категорию</option>{[...activeCategories].sort((left, right) => russianCollator.compare(left.name, right.name)).map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}</select></label>
        <label>Название вещи<input name="title" required maxLength={160} value={listingForm.title} onChange={(event) => setListingForm({ ...listingForm, title: event.target.value })} /></label>
        <label>Описание<textarea name="description" required rows={4} maxLength={5000} value={listingForm.description} onChange={(event) => setListingForm({ ...listingForm, description: event.target.value })} /></label>
        <label>Желаемая цена<input name="askingPrice" required type="number" min="0.01" step="0.01" value={listingForm.askingPrice} onChange={(event) => setListingForm({ ...listingForm, askingPrice: event.target.value })} /></label>
        <p className="muted">Минимальная цена зависит от выбранного магазина. Администратор проверит заявку и сообщит адрес передачи вещи.</p>
        <div className="button-row"><button className="button" disabled={listingBusy || !user}>{listingBusy ? 'Отправляем…' : 'Отправить заявку'}</button><button className="button-quiet" type="button" onClick={() => setView('listings')}>Назад</button></div>
      </form></section>}

      {view === 'admin' && <section className="page-section"><p className="eyebrow">Рабочее место</p><h1>Управление магазинами</h1>
        {user?.isSuperAdmin && <div className="admin-layout">
          <section className="panel"><div className="section-heading compact"><h2>{adminStoreId ? 'Настройки магазина' : 'Новый магазин'}</h2><button className="button-quiet" type="button" onClick={() => { setAdminStoreId(null); setStoreForm(makeStoreForm()) }}>＋ Новый</button></div>
            <form className="form-stack" onSubmit={saveStore} onInvalidCapture={handleInvalidField} onInputCapture={handleFormInput} onChangeCapture={handleFormInput}>
              <label>Название<input name="name" required maxLength={160} value={storeForm.name} onChange={(event) => setStoreForm({ ...storeForm, name: event.target.value })} /></label>
              <label>Адрес передачи товара<input name="address" required maxLength={500} value={storeForm.address} onChange={(event) => setStoreForm({ ...storeForm, address: event.target.value })} /></label>
              <div className="two-columns"><label>Валюта<input name="currencyCode" required minLength={3} maxLength={3} pattern="[A-Za-z]{3}" value={storeForm.currencyCode} onChange={(event) => setStoreForm({ ...storeForm, currencyCode: event.target.value.toUpperCase() })} /></label><label>Часовой пояс магазина<select name="timeZoneId" required value={storeForm.timeZoneId} onChange={(event) => setStoreForm({ ...storeForm, timeZoneId: event.target.value })}>{utcOffsetOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}</select></label></div>
              <label>Бронь, рабочие часы<input name="bookingDurationHours" required type="number" min="1" max="720" step="1" value={storeForm.bookingDurationHours} onChange={(event) => setStoreForm({ ...storeForm, bookingDurationHours: Number(event.target.value) })} /></label>
              <div className="subheading"><h3>Рабочие интервалы</h3><button type="button" className="button-quiet" onClick={() => setStoreForm({ ...storeForm, openingIntervals: [...storeForm.openingIntervals, { dayOfWeek: 1, opensAt: '10:00', closesAt: '19:00' }] })}>Добавить интервал</button></div>
              {storeForm.openingIntervals.map((interval, index) => <div className="interval-row" key={`${index}-${interval.dayOfWeek}`}>
                <label className="compact-field"><span>День</span><select name={`openingIntervals[${index}].dayOfWeek`} aria-label="День недели" value={interval.dayOfWeek} onChange={(event) => setIntervalValue(index, 'dayOfWeek', Number(event.target.value))}>{dayNames.map((day, dayIndex) => <option key={day} value={dayIndex}>{day}</option>)}</select></label>
                <label className="compact-field"><span>Открытие</span><input name={`openingIntervals[${index}].opensAt`} aria-label="Открытие" type="time" required value={interval.opensAt} onChange={(event) => setIntervalValue(index, 'opensAt', event.target.value)} /></label>
                <label className="compact-field"><span>Закрытие</span><input name={`openingIntervals[${index}].closesAt`} aria-label="Закрытие" type="time" required value={interval.closesAt} onChange={(event) => setIntervalValue(index, 'closesAt', event.target.value)} /></label>
                <button className="icon-button" type="button" aria-label="Удалить интервал" onClick={() => setStoreForm({ ...storeForm, openingIntervals: storeForm.openingIntervals.filter((_, itemIndex) => itemIndex !== index) })}>×</button>
              </div>)}
              <div className="subheading"><h3>Сетка комиссии</h3><button type="button" className="button-quiet" onClick={() => setStoreForm({ ...storeForm, commissionBands: [...storeForm.commissionBands, { priceThreshold: 0, kind: 'Percent', rate: 0 }] })}>Добавить порог</button></div>
              {storeForm.commissionBands.map((band, index) => <div className="interval-row commission-row" key={index}>
                <label>Порог<input name={`commissionBands[${index}].priceThreshold`} type="number" min="0" step="0.01" required value={band.priceThreshold} onChange={(event) => setBandValue(index, 'priceThreshold', event.target.value)} /></label>
                <select name={`commissionBands[${index}].kind`} aria-label="Тип комиссии" value={band.kind} onChange={(event) => setBandValue(index, 'kind', event.target.value)}><option value="Percent">Процент</option><option value="Fixed">Фиксированная сумма</option></select>
                <label>Ставка<input name={`commissionBands[${index}].rate`} type="number" min="0" step="0.01" required value={band.rate} onChange={(event) => setBandValue(index, 'rate', event.target.value)} /></label>
                <button className="icon-button" type="button" aria-label="Удалить порог" disabled={storeForm.commissionBands.length < 2} onClick={() => setStoreForm({ ...storeForm, commissionBands: storeForm.commissionBands.filter((_, itemIndex) => itemIndex !== index) })}>×</button>
              </div>)}
              <button className="button" disabled={storeBusy}>{storeBusy ? 'Сохраняем…' : 'Сохранить магазин'}</button>
            </form>
          </section>
          <section className="panel"><h2>Назначить администратора</h2><form className="form-stack" onSubmit={assignAdmin} onInvalidCapture={handleInvalidField} onInputCapture={handleFormInput} onChangeCapture={handleFormInput}>
            <label>Email зарегистрированного пользователя<input name="email" type="email" required value={adminEmail} onChange={(event) => setAdminEmail(event.target.value)} /></label>
            <p className="muted">Выберите магазины, которыми будет управлять этот пользователь.</p>
            {adminStores.map((store) => <label className="check-row" key={store.id}><input type="checkbox" checked={adminStoreIds.includes(store.id)} onChange={(event) => setAdminStoreIds(event.target.checked ? [...adminStoreIds, store.id] : adminStoreIds.filter((id) => id !== store.id))} />{store.name}</label>)}
            <button className="button" disabled={adminStores.length === 0}>Назначить</button>
          </form></section>
        </div>}
        {user?.isSuperAdmin && <section className="panel category-admin-panel">
          <div className="section-heading compact"><div><h2>Категории товаров</h2><p className="muted">Создавайте категории, переименовывайте их и скрывайте из новых заявок.</p></div></div>
          <form className="category-create-row" onSubmit={createCategory} onInvalidCapture={handleInvalidField} onInputCapture={handleFormInput} onChangeCapture={handleFormInput}>
            <label>Новая категория<input name="name" required maxLength={120} value={newCategoryName} onChange={(event) => setNewCategoryName(event.target.value)} /></label>
            <button className="button button-small" disabled={categoryBusy || !newCategoryName.trim()}>{categoryBusy ? 'Сохраняем…' : 'Добавить категорию'}</button>
          </form>
          <div className="category-admin-list">{adminCategories.map((category) => <AdminCategoryRow key={category.id} category={category} onSaved={async () => {
            await Promise.all([loadAdminCategories(), loadActiveCategories()])
            if (storeId) await loadCatalog(storeId)
          }} onNotice={showNotice} />)}</div>
        </section>}
        {adminStores.length > 0 ? <>
          <div className="section-heading admin-products-heading"><h2>Заявки и товары</h2><label className="store-filter">Магазин<select value={adminStoreId ?? ''} onChange={(event) => { const id = Number(event.target.value); selectManagedStore(id); void loadAdminProducts(id) }}>{adminStores.map((store) => <option key={store.id} value={store.id}>{store.name}</option>)}</select></label></div>
          {adminProducts.length === 0 ? <div className="empty-state"><h3>Заявок и товаров пока нет</h3></div> : <div className="admin-product-list">{adminProducts.map((product) => <AdminProductCard key={product.id} product={product} activeCategories={activeCategories} onChanged={async () => { if (adminStoreId) await loadAdminProducts(adminStoreId); if (storeId) await loadCatalog(storeId); await loadListings() }} onNotice={showNotice} />)}</div>}
        </> : !user?.isSuperAdmin && <div className="empty-state"><h3>Нет назначенных магазинов</h3><p>Попросите главного администратора назначить вам магазины.</p></div>}
      </section>}

      <footer className="footer"><span>Комок</span><span>Вещи продолжают жить</span><span className="photo-credit">Фон: <a href="https://commons.wikimedia.org/wiki/File:Cau_Thuan_Phuoc_ve_dem.JPG" target="_blank" rel="noreferrer">Thick thi sock, Wikimedia Commons</a>, <a href="https://creativecommons.org/licenses/by-sa/3.0/" target="_blank" rel="noreferrer">CC BY-SA 3.0</a> (кадрировано и затемнено)</span></footer>
    </main>
  )
}

function AdminCategoryRow({ category, onSaved, onNotice }: {
  category: Category
  onSaved: () => Promise<void>
  onNotice: (message: string, kind: NoticeKind) => void
}) {
  const [name, setName] = useState(category.name)
  const [isActive, setIsActive] = useState(category.isActive)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    setName(category.name)
    setIsActive(category.isActive)
  }, [category.name, category.isActive])

  async function save(nextIsActive: boolean, form?: HTMLFormElement) {
    setBusy(true)
    try {
      await api(`/api/admin/categories/${category.id}`, {
        method: 'PUT', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, isActive: nextIsActive }),
      })
      setIsActive(nextIsActive)
      await onSaved()
      onNotice('Категория сохранена.', 'success')
    } catch (error) {
      if (form) showFormFailure(form, error, 'Не удалось сохранить категорию.')
      else onNotice(error instanceof Error ? error.message : 'Не удалось сохранить категорию.', 'error')
    } finally {
      setBusy(false)
    }
  }

  function saveName(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    void save(isActive, event.currentTarget)
  }

  return <form className="category-admin-row" onSubmit={saveName} onInvalidCapture={handleInvalidField} onInputCapture={handleFormInput} onChangeCapture={handleFormInput}>
    <label>Название<input name="name" required maxLength={120} value={name} onChange={(event) => setName(event.target.value)} /></label>
    <span className={`category-status ${isActive ? 'is-active' : ''}`}>{isActive ? 'Активна' : 'Неактивна'}</span>
    <div className="button-row">
      <button className="button button-small" disabled={busy || !name.trim()}>Сохранить</button>
      <button className="button-quiet" type="button" disabled={busy} onClick={() => void save(!isActive)}>{isActive ? 'Отключить' : 'Включить'}</button>
    </div>
  </form>
}

function AdminProductCard({ product, activeCategories, onChanged, onNotice }: {
  product: AdminProduct
  activeCategories: Category[]
  onChanged: () => Promise<void>
  onNotice: (message: string, kind: NoticeKind) => void
}) {
  const [title, setTitle] = useState(product.title)
  const [description, setDescription] = useState(product.description)
  const [price, setPrice] = useState(String(product.askingPrice))
  const [categoryId, setCategoryId] = useState(String(product.categoryId))
  const [commissionPercent, setCommissionPercent] = useState(product.commissionKind === 'Percent' ? String(product.commissionRate ?? '') : '')
  const [salePrice, setSalePrice] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => setCategoryId(String(product.categoryId)), [product.categoryId])

  async function action(path: string, body?: unknown, form?: HTMLFormElement) {
    setBusy(true)
    try {
      await api(path, body === undefined ? { method: 'POST' } : {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
      })
      onNotice('Изменение сохранено. Продавцу отправлено email-уведомление.', 'success')
      await onChanged()
    } catch (error) {
      if (form) showFormFailure(form, error, 'Не удалось изменить товар.')
      else onNotice(error instanceof Error ? error.message : 'Не удалось изменить товар.', 'error')
    }
    finally { setBusy(false) }
  }

  function approveListing(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    void action(`/api/admin/products/${product.id}/review`, {
      action: 'approve', title, description, askingPrice: Number(price),
      commissionPercent: commissionPercent === '' ? null : Number(commissionPercent),
      categoryId: Number(categoryId),
    }, form)
  }

  function markSold(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    void action(`/api/admin/products/${product.id}/sold`, { soldPrice: Number(salePrice) }, form)
  }

  return <article className="admin-product panel">
    <div className="admin-product-title"><div><span className="badge">{listingLabels[product.listingStatus] ?? product.listingStatus} · {product.inventoryStatus}</span><h3>{product.title}</h3><p className="muted">Продавец: {product.sellerName ? `${product.sellerName} · ` : ''}{product.sellerEmail} · {product.sellerMessenger}: {product.sellerMessengerContact}</p></div><strong>{formatMoney(product.askingPrice, product.currencyCode)}</strong></div>
    {product.inventoryStatus === 'Reserved' && <div className="reservation-details"><p className="muted">Бронь до {formatDate(product.reservedUntil ?? null, product.timeZoneId)}</p><p className="muted">Прибытие на осмотр {formatDate(product.arrivalAt, product.timeZoneId)}</p>{(product.buyerName || (product.buyerMessenger && product.buyerMessengerContact)) && <p className="muted">{product.buyerName}{product.buyerName && product.buyerMessenger && product.buyerMessengerContact ? ' · ' : ''}{product.buyerMessenger && product.buyerMessengerContact ? `${product.buyerMessenger}: ${product.buyerMessengerContact}` : ''}</p>}</div>}
    {['Submitted', 'NeedsRevision'].includes(product.listingStatus) && <form className="form-stack review-fields" onSubmit={approveListing} onInvalidCapture={handleInvalidField} onInputCapture={handleFormInput} onChangeCapture={handleFormInput}>
      <label>Название<input name="title" required value={title} maxLength={160} onChange={(event) => setTitle(event.target.value)} /></label>
      <label>Описание<textarea name="description" required rows={3} value={description} maxLength={5000} onChange={(event) => setDescription(event.target.value)} /></label>
      <label>Категория<select name="categoryId" required value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
        {!activeCategories.some((category) => category.id === product.categoryId) && <option value={product.categoryId}>{product.category} (неактивна)</option>}
        {[...activeCategories].sort((left, right) => russianCollator.compare(left.name, right.name)).map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}
      </select></label>
      <div className="two-columns"><label>Цена<input name="askingPrice" type="number" required min="0.01" step="0.01" value={price} onChange={(event) => setPrice(event.target.value)} /></label><label>Комиссия, % (оставьте пустой для сетки)<input name="commissionPercent" type="number" min="0" max="100" step="0.01" value={commissionPercent} onChange={(event) => setCommissionPercent(event.target.value)} /></label></div>
      <div className="button-row"><button className="button button-small" type="submit" disabled={busy}>Одобрить и ждать доставку</button><button className="button button-outline button-small" type="button" disabled={busy} onClick={() => void action(`/api/admin/products/${product.id}/review`, { action: 'revision' })}>Вернуть на доработку</button><button className="button-danger" type="button" disabled={busy} onClick={() => void action(`/api/admin/products/${product.id}/review`, { action: 'reject' })}>Отказать</button></div>
    </form>}
    {product.listingStatus === 'Delivery' && <button className="button button-small" disabled={busy} onClick={() => void action(`/api/admin/products/${product.id}/delivery-received`)}>Подтвердить получение · на продажу</button>}
    {product.listingStatus === 'OnSale' && product.inventoryStatus !== 'Sold' && <form className="button-row sale-row" onSubmit={markSold} onInvalidCapture={handleInvalidField} onInputCapture={handleFormInput} onChangeCapture={handleFormInput}><label>Цена продажи<input name="soldPrice" type="number" required min="0.01" step="0.01" value={salePrice} onChange={(event) => setSalePrice(event.target.value)} /></label><button className="button button-small" type="submit" disabled={busy}>Отметить продажу</button></form>}
    {product.inventoryStatus === 'Sold' && product.payoutStatus === 'Pending' && <div className="payout-details"><p>Реквизиты продавца: {product.sellerPayoutDetails || 'не указаны'}</p><div className="button-row"><span>К выплате: {formatMoney(product.payoutAmount ?? 0, product.currencyCode)}</span><button className="button button-small" disabled={busy} onClick={() => void action(`/api/admin/products/${product.id}/payout`)}>Выплата произведена</button></div></div>}
    {product.payoutStatus === 'Paid' && <span className="badge badge-success">Выплата произведена</span>}
  </article>
}

function formatMoney(amount: number, currency: string) {
  try { return new Intl.NumberFormat('ru-RU', { style: 'currency', currency }).format(amount) }
  catch { return `${amount.toFixed(2)} ${currency}` }
}

const utcOffsetLocations: Record<string, string> = {
  '-12:00': 'острова Бейкер и Хауленд', '-11:00': 'Паго-Паго', '-10:00': 'Гонолулу',
  '-09:30': 'Маркизские острова', '-09:00': 'Гамбиер', '-08:00': 'Питкэрн',
  '-07:00': 'Финикс', '-06:00': 'Гватемала', '-05:00': 'Богота, Лима',
  '-04:00': 'Каракас', '-03:30': 'Сент-Джонс', '-03:00': 'Буэнос-Айрес',
  '-02:00': 'Фернанду-ди-Норонья', '-01:00': 'Прая', '+00:00': 'Рейкьявик',
  '+01:00': 'Лагос', '+02:00': 'Йоханнесбург', '+03:00': 'Москва',
  '+03:30': 'Тегеран', '+04:00': 'Дубай', '+04:30': 'Кабул', '+05:00': 'Ташкент',
  '+05:30': 'Калькутта', '+05:45': 'Катманду', '+06:00': 'Дакка', '+06:30': 'Янгон',
  '+07:00': 'Бангкок, Хошимин', '+08:00': 'Сингапур, Манила', '+08:45': 'Юкла',
  '+09:00': 'Токио', '+09:30': 'Дарвин', '+10:00': 'Брисбен, Владивосток',
  '+10:30': 'остров Лорд-Хау', '+11:00': 'Нумеа, Магадан', '+12:00': 'Тарава',
  '+12:45': 'острова Чатем', '+13:00': 'Тонга', '+14:00': 'Киритимати',
}

const utcOffsetOptions = Array.from({ length: (14 * 60 + 12 * 60) / 15 + 1 }, (_, index) => {
  const totalMinutes = -12 * 60 + index * 15
  const sign = totalMinutes < 0 ? '-' : '+'
  const absoluteMinutes = Math.abs(totalMinutes)
  const value = `${sign}${String(Math.floor(absoluteMinutes / 60)).padStart(2, '0')}:${String(absoluteMinutes % 60).padStart(2, '0')}`
  const location = utcOffsetLocations[value]
  return { value, label: `UTC${sign}${String(Math.floor(absoluteMinutes / 60))}:${String(absoluteMinutes % 60).padStart(2, '0')}${location ? ` — ${location}` : ''}` }
})

function formatDate(value: string | null, utcOffset: string) {
  if (!value) return ''
  const date = new Date(value)
  const match = /^([+-])(\d{2}):(\d{2})$/.exec(utcOffset)
  if (!match || Number.isNaN(date.getTime())) return ''
  const offsetMinutes = (Number(match[2]) * 60 + Number(match[3])) * (match[1] === '-' ? -1 : 1)
  const storeTime = new Date(date.getTime() + offsetMinutes * 60_000)
  return new Intl.DateTimeFormat('ru-RU', { dateStyle: 'short', timeStyle: 'short', timeZone: 'UTC' }).format(storeTime)
}

function formatRelativeArrival(value: string, now: number) {
  const remainingMilliseconds = Date.parse(value) - now
  if (!Number.isFinite(remainingMilliseconds) || remainingMilliseconds <= 0) return 'время прошло'

  const totalMinutes = Math.floor(remainingMilliseconds / 60_000)
  if (totalMinutes === 0) return 'через 1 мин.'

  const days = Math.floor(totalMinutes / (24 * 60))
  const hours = Math.floor((totalMinutes % (24 * 60)) / 60)
  const minutes = totalMinutes % 60
  const parts = [
    days > 0 ? `${days} д.` : '',
    hours > 0 ? `${hours} ч.` : '',
    minutes > 0 ? `${minutes} мин.` : '',
  ].filter(Boolean)

  return `через ${parts.join(' ')}`
}
