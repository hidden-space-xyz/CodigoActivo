import type { ResourceRepository } from '@/modules/resources/domain/repositories/resource-repository'

import { HttpResourceRepository } from './http-resource.repository'

export const resourceRepository: ResourceRepository = new HttpResourceRepository()
