BUILD_SLN=./jaytwo.NuGetCheck.sln
BUILD_DIRS=./src/jaytwo.NuGetCheck
BUILD_TEST_DIRS=./test/jaytwo.NuGetCheck.Tests

NUGET_SOURCE_URL?=https://api.nuget.org/v3/index.json
NUGET_API_KEY?=__missing_api_key__

TOPDIR=${CURDIR}
BUILD_TEST_RESULTS_DIR=${TOPDIR}/out/testResults
BUILD_TEST_COVERAGE_DIR=${TOPDIR}/out/coverage
BUILD_PACKED_DIR=${TOPDIR}/out/packed

DOCKER_TAG?=$(call getDockerTag,$(BUILD_SLN))
DOCKER_BASE_TAG?=${DOCKER_TAG}__base
DOCKER_BUILDER_TAG?=${DOCKER_TAG}__builder
DOCKER_BUILDER_CONTAINER?=${DOCKER_BUILDER_TAG}
DOCKER_RUN_MAKE_TARGETS?=run
TIMESTAMP?=$(call getTimestamp)

default: clean build

deps:
	dotnet tool install -g dotnet-reportgenerator-globaltool
	dotnet tool install -g jaytwo.NuGetCheck.GlobalTool

clean:
	find . -name bin | xargs --no-run-if-empty rm -vrf
	find . -name obj | xargs --no-run-if-empty rm -vrf
	rm -rf ${TOPDIR}/out

restore:
	dotnet restore . --verbosity minimal

build: restore
	dotnet build "${BUILD_SLN}"

test: unit-test

unit-test: build
	rm -rf "${BUILD_TEST_RESULTS_DIR}"
	rm -rf "${BUILD_TEST_COVERAGE_DIR}"
	for dir in $$(echo "${BUILD_TEST_DIRS}" | tr ':' '\n'); do \
		[ -n "$$dir" ] \
			&& cd "${TOPDIR}" \
			&& cd "$$dir" \
			&& dotnet test \
				--results-directory "${BUILD_TEST_RESULTS_DIR}" \
				--logger "trx;LogFileName=$$(basename $$dir).trx"; \
	done
	reportgenerator \
		"-reports:${BUILD_TEST_COVERAGE_DIR}/**/coverage.cobertura.xml" \
		"-targetdir:${BUILD_TEST_COVERAGE_DIR}/" \
		"-reportTypes:Cobertura"
	reportgenerator \
		"-reports:${BUILD_TEST_COVERAGE_DIR}/**/coverage.cobertura.xml" \
		"-targetdir:${BUILD_TEST_COVERAGE_DIR}/html" \
		"-reportTypes:Html"

pack:
	rm -rf "${BUILD_PACKED_DIR}";
	for dir in $$(echo "${BUILD_DIRS}" | tr ':' '\n'); do \
		[ -n "$$dir" ] \
			&& cd "${TOPDIR}" \
			&& cd "$$dir" \
			&& dotnet pack -o "${BUILD_PACKED_DIR}" ${PACK_ARG}; \
	done

pack-beta: PACK_ARG=--version-suffix beta-${TIMESTAMP}
pack-beta: pack

nuget-check:
	PACKED_NUPKG_FILES="$(call getNupkgFiles)"; \
	for nupkg in $$PACKED_NUPKG_FILES; do \
		if [ -n "$$nupkg" ]; then \
			nugetcheck \
				"$$nupkg" \
				-gte "$$nupkg" \
				--same-major \
				--fail-on-match \
			&& echo "$$(basename $$nupkg): Ready to push!" \
			|| echo "$$(basename $$nupkg): Failed NuGetCheck; cannot push."; \
		fi; \
	done

nuget-push: nuget-check
nuget-push:
	PACKED_NUPKG_FILES="$(call getNupkgFiles)"; \
	for nupkg in $$PACKED_NUPKG_FILES; do \
		if [ -n "$$nupkg" ]; then \
			dotnet nuget push \
				"$$PACKED_NUPKG_FILE" \
				--source "${NUGET_SOURCE_URL}" \
				--api-key "$$NUGET_API_KEY"; \
		fi; \
	done

docker-builder:
	# building the base image to force caching those layers in an otherwise discarded stage of the multistage dockerfile
	docker build -t ${DOCKER_BASE_TAG} . --target base --pull
	docker build -t ${DOCKER_BUILDER_TAG} . --target builder --pull

docker: docker-builder
	docker build -t ${DOCKER_TAG} . --pull

docker-run:
	docker run --name ${DOCKER_BUILDER_CONTAINER} ${DOCKER_BUILDER_TAG} make ${DOCKER_RUN_MAKE_TARGETS} || EXIT_CODE=$$? ; \
	docker cp ${DOCKER_BUILDER_CONTAINER}:build/out ./ || echo "Could not copy out of builder container: Container not found: ${DOCKER_BUILDER_CONTAINER}"; \
	docker rm ${DOCKER_BUILDER_CONTAINER} && echo "Container removed: ${DOCKER_BUILDER_CONTAINER}" || echo "Container not found: ${DOCKER_BUILDER_CONTAINER}"; \
	exit $$EXIT_CODE

docker-copy-from-builder-output:
	docker cp ${DOCKER_BUILDER_CONTAINER}:build/out ./ || echo "Could not copy out of builder container: Container not found: ${DOCKER_BUILDER_CONTAINER}"

docker-test: DOCKER_RUN_MAKE_TARGETS=test
docker-test: docker-run

docker-pack: DOCKER_RUN_MAKE_TARGETS=pack
docker-pack: docker-run

docker-pack: DOCKER_RUN_MAKE_TARGETS=pack-beta
docker-pack: docker-run

docker-clean:
	docker rm ${DOCKER_BUILDER_CONTAINER} && echo "Container removed: ${DOCKER_BUILDER_CONTAINER}" || echo  "Nothing to clean up for: ${DOCKER_BUILDER_CONTAINER}"
	# not removing image DOCKER_BASE_TAG since we want the layer cache to stick around (hopefully they will be cleaned up on the scheduled job)
	docker rmi ${DOCKER_BUILDER_TAG} && echo "Image removed: ${DOCKER_BUILDER_TAG}" || echo "Nothing to clean up for: ${DOCKER_BUILDER_TAG}"
	docker rmi ${DOCKER_TAG} && echo "Image removed: ${DOCKER_TAG}" || echo "Nothing to clean up for: ${DOCKER_TAG}"

define getDockerTag
$(shell echo '$(basename $(1))' | tr '[:upper:]' '[:lower:]' | sed 's/[^a-z0-9]/_/g' | sed 's/^_*//')
endef

define getTimestamp
$(shell date +'%Y%m%d%H%M%S')
endef

define getNupkgFiles
$(shell ls -1 ${BUILD_PACKED_DIR}/*.nupkg)
endef
