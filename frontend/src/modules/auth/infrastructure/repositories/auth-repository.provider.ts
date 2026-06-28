import type { AuthRepository } from '@/modules/auth/domain/repositories/auth-repository'

import { HttpAuthRepository } from './http-auth.repository'

export const authRepository: AuthRepository = new HttpAuthRepository()
