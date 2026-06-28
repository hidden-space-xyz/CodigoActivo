import type { AccountRepository } from '@/modules/account/domain/repositories/account-repository'

import { HttpAccountRepository } from './http-account.repository'

export const accountRepository: AccountRepository = new HttpAccountRepository()
