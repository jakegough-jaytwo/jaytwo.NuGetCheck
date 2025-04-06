TIMESTAMP?=$(shell date +'%Y%m%d%H%M%S')
DOCKER_TAG?=jaytwo_nugetcheck
NUGET_PACKAGE?=jaytwo.NuGetCheck
NUGET_SOURCE_URL?=https://api.nuget.org/v3/index.json
NUGET_API_KEY?=__missing_api_key__

default: clean build

deps:
	dotnet tool install -g dotnet-reportgenerator-globaltool
	dotnet tool install -g jaytwo.NuGetCheck

clean: 
	find . -name bin | xargs --no-run-if-empty rm -vrf
	find . -name obj | xargs --no-run-if-empty rm -vrf
	rm -rf out

restore:
	dotnet restore . --verbosity minimal
  
build: restore
	dotnet build ./jaytwo.NuGetCheck.sln

test: unit-test
  
unit-test: build
	rm -rf out/testResults
	rm -rf out/coverage
	cd ./test/jaytwo.NuGetCheck.Tests; \
		dotnet test \
		--results-directory ../../out/testResults \
		--logger "trx;LogFileName=jaytwo.NuGetCheck.Tests.trx"
	reportgenerator \
		-reports:./out/coverage/**/coverage.cobertura.xml \
		-targetdir:./out/coverage/ \
		-reportTypes:Cobertura
	reportgenerator \
		-reports:./out/coverage/**/coverage.cobertura.xml \
		-targetdir:./out/coverage/html \
		-reportTypes:Html

pack:
	rm -rf out/packed
	cd ./src/jaytwo.NuGetCheck; \
		dotnet pack -o ../../out/packed ${PACK_ARG}

pack-beta: PACK_ARG=--version-suffix beta-${TIMESTAMP}
pack-beta: pack

PACKED_NUPKG_FILE?=$(shell ls -1 'out/packed/*.nupkg')
nuget-check:
	nugetcheck ${NUGET_PACKAGE} -gte ${PACKED_NUPKG_FILE} --same-major --opposite-day

nuget-push: nuget-check
nuget-push:
	dotnet nuget push "${PACKED_NUPKG_FILE}" --source "${NUGET_SOURCE_URL}" --api-key "${NUGET_API_KEY}"

DOCKER_BASE_TAG?=${DOCKER_TAG}__base
DOCKER_BUILDER_TAG?=${DOCKER_TAG}__builder
DOCKER_BUILDER_CONTAINER?=${DOCKER_BUILDER_TAG}
docker-builder:
	# building the base image to force caching those layers in an otherwise discarded stage of the multistage dockerfile
	docker build -t ${DOCKER_BASE_TAG} . --target base --pull
	docker build -t ${DOCKER_BUILDER_TAG} . --target builder --pull

docker: docker-builder
	docker build -t ${DOCKER_TAG} . --pull
 
DOCKER_RUN_MAKE_TARGETS?=run
docker-run:
	docker run --name ${DOCKER_BUILDER_CONTAINER} ${DOCKER_BUILDER_TAG} make ${DOCKER_RUN_MAKE_TARGETS} || EXIT_CODE=$$? ; \
	docker cp ${DOCKER_BUILDER_CONTAINER}:build/out ./ || echo "Container not found: ${DOCKER_BUILDER_CONTAINER}"; \
	docker rm ${DOCKER_BUILDER_CONTAINER} || echo "Container not found: ${DOCKER_BUILDER_CONTAINER}"}; \
	exit $$EXIT_CODE

docker-unit-test-only: DOCKER_RUN_MAKE_TARGETS=unit-test
docker-unit-test-only: docker-run

docker-unit-test: docker-builder docker-unit-test-only

docker-pack-only: DOCKER_RUN_MAKE_TARGETS=pack
docker-pack-only: docker-run

docker-pack: docker-builder docker-pack-only

docker-pack-beta-only: DOCKER_RUN_MAKE_TARGETS=pack-beta
docker-pack-beta-only: docker-run

docker-pack-beta: docker-builder docker-pack-beta-only

docker-clean:
	docker rm ${DOCKER_BUILDER_CONTAINER} || echo  "Nothing to clean up for: ${DOCKER_BUILDER_CONTAINER}"
	# not removing image DOCKER_BASE_TAG since we want the layer cache to stick around (hopefully they will be cleaned up on the scheduled job)
	docker rmi ${DOCKER_BUILDER_TAG} || echo "Nothing to clean up for: ${DOCKER_BUILDER_TAG}"
	docker rmi ${DOCKER_TAG} || echo "Nothing to clean up for: ${DOCKER_TAG}"
